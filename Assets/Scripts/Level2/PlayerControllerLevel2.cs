﻿using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerLevel2 : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float runFastMultiplier = 1.5f;
    [SerializeField] private float jumpForce = 60f;
    [SerializeField] private float doubleJumpForce = 50f;
    [SerializeField] private int maxJumpCount = 2;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Camera mainCamera;

    [Header("Combat Settings")]
    [SerializeField] private float comboResetTime = 0.6f;
    [SerializeField] private float comboDelayAfterFinish = 1.0f;
    [SerializeField] private int maxHealth = 120; // Tăng máu cho level 2
    [SerializeField] private int currentHealth;
    [SerializeField] private int lives = 2; // Tăng số mạng cho level 2

    [Header("Rage System")]
    [SerializeField] private int maxRagePoints = 60; // Tăng rage tối đa cho level 2
    [SerializeField] private int minRageToActivate = 45; // Tăng yêu cầu rage cho ulti
    [SerializeField] private int currentRagePoints = 0;
    [SerializeField] private int rageGainPerHit = 6; // Tăng rage gain cho level 2
    [SerializeField] private int rageGainPerSuccessfulHit = 7; // Tăng rage gain khi đánh boss

    [Header("Animation & Idle")]
    [SerializeField] private float idleThreshold = 8f; // Giảm thời gian idle để game dynamic hơn

    [Header("Hitbox & Hurtbox References")]
    public BoxCollider2D hitBoxCollider;
    public PlayerHitBoxHandle hitBoxHandle;
    public Transform hitBoxTransform;
    public Transform hurtBoxTransform;

    [Header("Attack Points & Effects")]
    public Transform attackPoint;
    public Transform attackPointUlti;
    public Transform attackPoint_ChemChuong; // THÊM DÒNG NÀY - attack point riêng cho chém chưởng
    public GameObject EffectChem1;
    public GameObject Effect_Chuong;
    public GameObject Effect_ChemChuong; // THÊM DÒNG NÀY - effect chém chưởng
    public GameObject UltiEffect;

    [Header("Level 2 New Features")]
    [SerializeField] private float dashDistance = 3f; // Kỹ năng dash mới
    [SerializeField] private float dashCooldown = 2f;
    [SerializeField] private int dashRageCost = 10; // Chi phí rage để dash
    [SerializeField] private bool canWallJump = true; // Cho phép wall jump ở level 2
    [SerializeField] private LayerMask wallLayer;

    // Core Components
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Transform groundCheck;
    private PlayerHurtBoxHandle hurtBoxHandle;

    // Movement & State
    private Vector2 moveInput;
    private bool isRunningFast;
    private bool isGrounded;
    private bool isUltimateActive = false;
    private int currentJumpCount = 0;
    private bool isBlocking = false;
    private bool isDead = false;
    private float idleTime = 0f;
    private bool isIdleForLong = false;

    // Combo System
    private int comboStepJ = 0;
    private int comboStepK = 0;
    private float lastAttackTimeJ = 0f;
    private float lastAttackTimeK = 0f;
    private bool isAttackingJ = false;
    private bool isAttackingK = false;
    private bool isComboWindowOpenJ = false;
    private bool isComboWindowOpenK = false;
    private bool hasBufferedInputJ = false;
    private bool hasBufferedInputK = false;

    // Combo Delay System
    private float comboFinishedTimeJ = 0f;
    private float comboFinishedTimeK = 0f;
    private bool isComboDelayActiveJ = false;
    private bool isComboDelayActiveK = false;

    // Level 2 New Features
    private float lastDashTime = 0f;
    private bool isDashing = false;
    private bool isWallTouching = false;
    private bool canWallJumpNow = false;

    // Properties
    public bool IsDead => isDead;
    public int CurrentHealth
    {
        get => currentHealth;
        private set
        {
            currentHealth = Mathf.Clamp(value, 0, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
    public int MaxHealth => maxHealth;
    public int CurrentRagePoints => currentRagePoints;
    public int MaxRagePoints => maxRagePoints;

    // Events
    public delegate void HealthChangeHandler(int currentHealth, int maxHealth);
    public event HealthChangeHandler OnHealthChanged;

    public delegate void RageChangeHandler(int currentRage, int maxRage);
    public event RageChangeHandler OnRageChanged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;

        groundCheck = transform.Find("GroundCheck");
        if (groundCheck == null)
        {
            Debug.LogError("GroundCheck transform is missing. Please add a child object named 'GroundCheck' to the player.");
        }

        // Disable hitbox at start
        if (hitBoxHandle != null)
        {
            hitBoxHandle.DisableHitbox();
        }
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (hurtBoxTransform != null)
            hurtBoxHandle = hurtBoxTransform.GetComponent<PlayerHurtBoxHandle>();

        DisablePlayerHitbox();
    }

    private void Update()
    {
        if (isDead) return;

        HandleGroundDetection();
        HandleMovementInput();
        HandleAnimationStates();
        HandleIdleState();
        HandleJumpInput();
        HandleComboDelayStatus();
        HandleCombatInput();
        HandleUltimate();
        HandleLevel2Features(); // New features for level 2
        HandleComboResets();
    }

    private void HandleGroundDetection()
    {
        bool wasGrounded = isGrounded;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer);

        // Wall detection for wall jump
        if (canWallJump)
        {
            isWallTouching = Physics2D.OverlapCircle(transform.position, 0.5f, wallLayer);
            canWallJumpNow = !isGrounded && isWallTouching && currentJumpCount < maxJumpCount;
        }

        if (!wasGrounded && isGrounded)
        {
            animator.ResetTrigger("Jump");
        }

        if (isGrounded)
        {
            currentJumpCount = 0;
        }
    }

    private void HandleMovementInput()
    {
        float moveX = Input.GetAxis("Horizontal");
        moveInput = new Vector2(moveX, 0).normalized;
        isRunningFast = Input.GetKey(KeyCode.LeftShift);
    }

    private void HandleAnimationStates()
    {
        animator.SetFloat("Speed", Mathf.Abs(moveInput.x));
        animator.SetBool("IsRunningFast", isRunningFast);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsWallTouching", isWallTouching); // New animation parameter
    }

    private void HandleIdleState()
    {
        if (Mathf.Abs(moveInput.x) == 0 && !Input.anyKey && isGrounded)
        {
            idleTime += Time.deltaTime;
            if (idleTime >= idleThreshold && !isIdleForLong)
            {
                isIdleForLong = true;
                animator.SetTrigger("IdleForLong");
            }
        }
        else
        {
            idleTime = 0f;
            isIdleForLong = false;
        }
    }

    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (canWallJumpNow)
            {
                WallJump();
            }
            else
            {
                Jump();
            }
        }
    }

    private void HandleComboDelayStatus()
    {
        UpdateComboDelayStatus();
    }

    private void HandleCombatInput()
    {
        // Kiểm tra combo J+U trước (ưu tiên cao nhất)
        if (Input.GetKey(KeyCode.J) && Input.GetKey(KeyCode.U))
        {
            // Chỉ trigger khi cả hai key đều được nhấn trong frame hiện tại
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.U))
            {
                animator.SetTrigger("Attack3+");
                return; // Quan trọng: return để không xử lý các attack khác
            }
        }

        // Combo J - chỉ xử lý khi không có combo J+U
        if (Input.GetKeyDown(KeyCode.J) && !Input.GetKey(KeyCode.U) && !isComboDelayActiveJ)
        {
            TryAttackJ();
        }

        // Combo K
        if (Input.GetKeyDown(KeyCode.K) && !isComboDelayActiveK)
        {
            TryAttackK();
        }

        // Attack U - chỉ xử lý khi không có combo J+U
        if (Input.GetKeyDown(KeyCode.U) && !Input.GetKey(KeyCode.J))
        {
            animator.SetTrigger("Attack3");
        }

        // Blocking
        if (Input.GetKey(KeyCode.L))
        {
            HandleBlock();
        }
        else if (Input.GetKeyUp(KeyCode.L))
        {
            StopBlock();
        }
    }

    private void HandleLevel2Features()
    {
        // Dash ability (Q key)
        if (Input.GetKeyDown(KeyCode.Q) && CanDash())
        {
            PerformDash();
        }
    }

    private void HandleComboResets()
    {
        if (isAttackingJ && Time.time - lastAttackTimeJ > comboResetTime && !hasBufferedInputJ)
        {
            ResetComboJ();
        }
        if (isAttackingK && Time.time - lastAttackTimeK > comboResetTime && !hasBufferedInputK)
        {
            ResetComboK();
        }
    }

    private void FixedUpdate()
    {
        if (!isDead && !isDashing)
        {
            MovePlayer();
        }
    }

    private void LateUpdate()
    {
        if (hitBoxHandle != null)
            hitBoxHandle.FlipHitbox(!spriteRenderer.flipX);
    }

    // Wall Jump Feature (New for Level 2)
    private void WallJump()
    {
        if (!canWallJumpNow) return;

        float wallJumpForce = jumpForce * 0.8f;
        float wallJumpHorizontal = moveInput.x != 0 ? -moveInput.x : (spriteRenderer.flipX ? 1 : -1);

        rb.linearVelocity = new Vector2(wallJumpHorizontal * 3f, wallJumpForce);

        animator.SetTrigger("WallJump"); // New animation trigger
        currentJumpCount++;

        Debug.Log("Wall jump performed!");
    }

    // Dash Feature (New for Level 2)
    private bool CanDash()
    {
        return Time.time - lastDashTime >= dashCooldown &&
               currentRagePoints >= dashRageCost &&
               !isDashing &&
               !isBlocking;
    }

    private void PerformDash()
    {
        if (!CanDash()) return;

        StartCoroutine(DashCoroutine());

        // Consume rage
        currentRagePoints -= dashRageCost;
        OnRageChanged?.Invoke(currentRagePoints, maxRagePoints);

        lastDashTime = Time.time;

        Debug.Log("Dash performed!");
    }

    private IEnumerator DashCoroutine()
    {
        isDashing = true;
        animator.SetTrigger("Dash"); // New animation trigger

        float dashDirection = spriteRenderer.flipX ? -1 : 1;
        if (moveInput.x != 0)
        {
            dashDirection = moveInput.x > 0 ? 1 : -1;
        }

        // Perform dash movement
        Vector2 dashVelocity = new Vector2(dashDirection * dashDistance * 10f, 0);
        rb.linearVelocity = dashVelocity;

        yield return new WaitForSeconds(0.2f);

        // Gradually reduce velocity
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.1f, rb.linearVelocity.y);

        isDashing = false;
    }

    // Enhanced Movement System
    void MovePlayer()
    {
        if (isBlocking || isDashing)
        {
            if (!isDashing)
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        float currentSpeed = isRunningFast ? speed * runFastMultiplier : speed;
        Vector2 movement = new Vector2(moveInput.x * currentSpeed, rb.linearVelocity.y);

        // Screen bounds calculation
        float halfWidth = spriteRenderer.bounds.extents.x;
        float cameraHalfWidth = mainCamera.orthographicSize * mainCamera.aspect;
        float leftLimit = mainCamera.transform.position.x - cameraHalfWidth + halfWidth;
        float rightLimit = mainCamera.transform.position.x + cameraHalfWidth - halfWidth;

        float newX = transform.position.x + movement.x * Time.fixedDeltaTime;
        newX = Mathf.Clamp(newX, leftLimit, rightLimit);

        rb.linearVelocity = new Vector2((newX - transform.position.x) / Time.fixedDeltaTime, movement.y);

        // Sprite flipping
        if (moveInput.x < 0)
        {
            spriteRenderer.flipX = true;
            FlipTransforms(false);
        }
        else if (moveInput.x > 0)
        {
            spriteRenderer.flipX = false;
            FlipTransforms(true);
        }
    }

    private void FlipTransforms(bool facingRight)
    {
        if (hitBoxHandle != null)
            hitBoxHandle.FlipHitbox(facingRight);
        if (hurtBoxHandle != null)
            hurtBoxHandle.FlipHurtbox(facingRight);

        FlipAttackPoint(attackPoint, facingRight);
        FlipAttackPoint(attackPointUlti, facingRight);
        FlipAttackPoint(attackPoint_ChemChuong, facingRight); // THÊM DÒNG NÀY
    }

    private void FlipAttackPoint(Transform point, bool facingRight)
    {
        if (point != null)
        {
            Vector3 localPos = point.localPosition;
            localPos.x = facingRight ? Mathf.Abs(localPos.x) : -Mathf.Abs(localPos.x);
            point.localPosition = localPos;
        }
    }

    // Enhanced Jump System
    void Jump()
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            animator.ResetTrigger("Jump");
            animator.SetTrigger("Jump");
            currentJumpCount = 1;
        }
        else if (currentJumpCount < maxJumpCount)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, doubleJumpForce);
            animator.ResetTrigger("Jump");
            animator.SetTrigger("Jump");
            currentJumpCount++;
        }
    }

    // Combat System (giữ nguyên logic từ Level 1)
    private void UpdateComboDelayStatus()
    {
        if (isComboDelayActiveJ && Time.time - comboFinishedTimeJ >= comboDelayAfterFinish)
        {
            isComboDelayActiveJ = false;
            Debug.Log("Combo J delay ended - can attack again");
        }

        if (isComboDelayActiveK && Time.time - comboFinishedTimeK >= comboDelayAfterFinish)
        {
            isComboDelayActiveK = false;
            Debug.Log("Combo K delay ended - can attack again");
        }
    }

    // Combo J System
    void TryAttackJ()
    {
        if (isAttackingJ)
        {
            if (isComboWindowOpenJ)
            {
                ContinueComboJ();
            }
            else
            {
                hasBufferedInputJ = true;
            }
        }
        else
        {
            StartComboJ();
        }
    }

    void StartComboJ()
    {
        comboStepJ = 1;
        isAttackingJ = true;
        lastAttackTimeJ = Time.time;
        animator.SetTrigger("Attack1");
    }

    void ContinueComboJ()
    {
        comboStepJ++;
        lastAttackTimeJ = Time.time;
        isComboWindowOpenJ = false;
        hasBufferedInputJ = false;

        if (comboStepJ == 2)
        {
            animator.SetTrigger("Attack1+");
            StartComboDelayJ();
        }
    }

    public void OpenComboWindowJ()
    {
        isComboWindowOpenJ = true;
        if (hasBufferedInputJ)
        {
            ContinueComboJ();
        }
    }

    public void CloseComboWindowJ()
    {
        isComboWindowOpenJ = false;
    }

    public void OnAttackAnimationEndJ()
    {
        if (comboStepJ >= 2 || !hasBufferedInputJ)
        {
            ResetComboJ();
        }
    }

    void ResetComboJ()
    {
        comboStepJ = 0;
        isAttackingJ = false;
        isComboWindowOpenJ = false;
        hasBufferedInputJ = false;
    }

    void StartComboDelayJ()
    {
        comboFinishedTimeJ = Time.time;
        isComboDelayActiveJ = true;
        Debug.Log($"Combo J finished - delay started for {comboDelayAfterFinish} seconds");
    }

    // Combo K System
    void TryAttackK()
    {
        if (isAttackingK)
        {
            if (isComboWindowOpenK)
            {
                ContinueComboK();
            }
            else
            {
                hasBufferedInputK = true;
            }
        }
        else
        {
            StartComboK();
        }
    }

    void StartComboK()
    {
        comboStepK = 1;
        isAttackingK = true;
        lastAttackTimeK = Time.time;
        animator.SetTrigger("Attack2");
    }

    void ContinueComboK()
    {
        comboStepK++;
        lastAttackTimeK = Time.time;
        isComboWindowOpenK = false;
        hasBufferedInputK = false;

        if (comboStepK == 2)
        {
            animator.SetTrigger("Attack2+");
            StartComboDelayK();
        }
    }

    public void OpenComboWindowK()
    {
        isComboWindowOpenK = true;
        if (hasBufferedInputK)
        {
            ContinueComboK();
        }
    }

    public void CloseComboWindowK()
    {
        isComboWindowOpenK = false;
    }

    public void OnAttackAnimationEndK()
    {
        if (comboStepK >= 2 || !hasBufferedInputK)
        {
            ResetComboK();
        }
    }

    void ResetComboK()
    {
        comboStepK = 0;
        isAttackingK = false;
        isComboWindowOpenK = false;
        hasBufferedInputK = false;
    }

    void StartComboDelayK()
    {
        comboFinishedTimeK = Time.time;
        isComboDelayActiveK = true;
        Debug.Log($"Combo K finished - delay started for {comboDelayAfterFinish} seconds");
    }

    // Block System
    void HandleBlock()
    {
        isBlocking = true;
        animator.SetBool("IsBlocking", true);
        rb.linearVelocity = Vector2.zero;
    }

    void StopBlock()
    {
        isBlocking = false;
        animator.SetBool("IsBlocking", false);
    }

    // Health & Damage System
    void HandleHit()
    {
        animator.SetTrigger("IsHit");
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        CurrentHealth = currentHealth - damage;

        if (CurrentHealth <= 0)
        {
            Die();
        }
        else
        {
            HandleHit();
        }
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        CurrentHealth = currentHealth + amount;
        Debug.Log($"Healed {amount}. Current Health: {currentHealth}/{maxHealth}");
    }

    public void Die()
    {
        if (isDead) return;

        isDead = true;
        animator.SetTrigger("IsDead");
        rb.linearVelocity = Vector2.zero;
        enabled = false;

        if (lives > 0)
        {
            lives--;
            Invoke(nameof(Respawn), 3f);
        }
        else
        {
            GameManager.Instance?.GameOver();
        }
    }

    private void Respawn()
    {
        isDead = false;
        enabled = true;
        CurrentHealth = maxHealth / 2;
        animator.SetTrigger("StandUp");
        rb.linearVelocity = Vector2.zero;
        transform.position = new Vector3(0, 0, 0);
    }

    // Ultimate System - ĐỔI SANG TRIGGER
    private void HandleUltimate()
    {
        if (Input.GetKeyDown(KeyCode.I) && currentRagePoints >= minRageToActivate && !isUltimateActive)
        {
            ActivateUltimate();
        }
    }

    private void ActivateUltimate()
    {
        if (currentRagePoints >= minRageToActivate && !isUltimateActive)
        {
            isUltimateActive = true;
            
            // ĐỔI THÀNH TRIGGER - SẼ TỰ ĐỘNG RESET SAU KHI TRIGGER
            animator.SetTrigger("IsUlti");

            currentRagePoints -= minRageToActivate;
            if (currentRagePoints < 0) currentRagePoints = 0;
            
            // Cập nhật rage UI
            OnRageChanged?.Invoke(currentRagePoints, maxRagePoints);

            StartCoroutine(DeactivateUltimateAfterDelay(5f));

            Debug.Log($"Ultimate activated! Rage consumed: {minRageToActivate}. Current rage: {currentRagePoints}");
        }
        else
        {
            Debug.Log($"Cannot activate Ultimate. Current rage: {currentRagePoints}, Required: {minRageToActivate}, IsActive: {isUltimateActive}");
        }
    }

    private IEnumerator DeactivateUltimateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isUltimateActive = false;
        
        Debug.Log("Ultimate deactivated - ready for next use");
    }

    // THÊM METHOD NÀY - Gọi từ Animation Event khi Ultimate animation bắt đầu
    public void OnUltimateAnimationStart()
    {
        Debug.Log("Ultimate animation started");
    }

    // THÊM METHOD NÀY - Gọi từ Animation Event khi Ultimate animation kết thúc
    public void OnUltimateAnimationEnd()
    {
        Debug.Log("Ultimate animation ended");
        // Trigger tự động reset, không cần manual reset
    }

    // THÊM METHOD NÀY - Để force reset Ultimate nếu cần thiết
    public void ForceResetUltimate()
    {
        isUltimateActive = false;
        Debug.Log("Ultimate force reset");
    }

    // Rage System
    public void AddRagePoints(int points)
    {
        int oldRage = currentRagePoints;
        currentRagePoints = Mathf.Min(currentRagePoints + points, maxRagePoints);

        if (currentRagePoints != oldRage)
        {
            Debug.Log($"Rage gained: +{points}. Current rage: {currentRagePoints}/{maxRagePoints}");
            OnRageChanged?.Invoke(currentRagePoints, maxRagePoints);
        }
    }

    // Hitbox Management
    public void EnablePlayerHitbox()
    {
        if (hitBoxHandle != null)
        {
            hitBoxHandle.EnableHitbox();
            hitBoxHandle.FlipHitbox(!spriteRenderer.flipX);
        }
    }

    public void DisablePlayerHitbox()
    {
        if (hitBoxHandle != null)
            hitBoxHandle.DisableHitbox();
    }

    // Effect System (giữ nguyên để tương thích với boss)
    public void SpawnSkillEffect(string effectName)
    {
        GameObject prefab = null;
        Transform spawnPoint = attackPoint;

        switch (effectName)
        {
            case "Attack1":
                prefab = EffectChem1;
                spawnPoint = attackPoint;
                break;
            case "Attack3":
                prefab = Effect_Chuong;
                spawnPoint = attackPoint;
                break;
            case "Attack3+":
                prefab = Effect_ChemChuong;
                spawnPoint = attackPoint_ChemChuong != null ? attackPoint_ChemChuong : attackPoint;
                break;
            case "Ultimate":
                prefab = UltiEffect;
                spawnPoint = attackPointUlti != null ? attackPointUlti : attackPoint;
                break;
            default:
                Debug.LogWarning("Skill effect not found: " + effectName);
                return;
        }

        if (prefab != null && spawnPoint != null)
        {
            GameObject effect = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

            // PLAYER LẬT SCALE CHO TẤT CẢ EFFECT
            float direction = spriteRenderer.flipX ? -1 : 1;
            effect.transform.localScale = new Vector3(direction, 1, 1);

            Debug.Log($"Spawned {effectName} effect facing {(spriteRenderer.flipX ? "LEFT" : "RIGHT")}");
        }
    }

    // Animation Event Methods - CẬP NHẬT ĐỂ ĐẢM BẢO HƯỚNG ĐÚNG
    public void SpawnUlti()
    {
        SpawnSkillEffect("Ultimate");
        Debug.Log("Ultimate effect spawned from animator event!");
    }

    public void SpawnUltiAtPoint()
    {
        if (UltiEffect != null)
        {
            Transform spawnPoint = attackPointUlti != null ? attackPointUlti : attackPoint;

            if (spawnPoint != null)
            {
                GameObject effect = Instantiate(UltiEffect, spawnPoint.position, Quaternion.identity);

                // ĐẢM BẢO HƯỚNG ĐÚNG
                float direction = spriteRenderer.flipX ? -1 : 1;
                effect.transform.localScale = new Vector3(direction, 1, 1);

                Debug.Log("Ultimate effect spawned at specific point from animator!");
            }
        }
    }

    public void SpawnChemChuong()
    {
        SpawnSkillEffect("Attack3+");
        Debug.Log("ChemChuong effect spawned from animator event!");
    }

    // Method để spawn Effect_Chuong (chiêu U)
    public void SpawnChuong()
    {
        SpawnSkillEffect("Attack3");
        Debug.Log("Chuong effect spawned from animator event!");
    }

    // Method để spawn EffectChem1 (chiêu J)
    public void SpawnChem1()
    {
        SpawnSkillEffect("Attack1");
        Debug.Log("Chem1 effect spawned from animator event!");
    }

    // Method để spawn ChemChuong tại attack point riêng với hướng chính xác
    public void SpawnChemChuongAtPoint()
    {
        if (Effect_ChemChuong != null)
        {
            Transform spawnPoint = attackPoint_ChemChuong != null ? attackPoint_ChemChuong : attackPoint;

            if (spawnPoint != null)
            {
                GameObject effect = Instantiate(Effect_ChemChuong, spawnPoint.position, Quaternion.identity);

                // ĐẢM BẢO HƯỚNG ĐÚNG CHO CHÉM CHƯỞNG
                float direction = spriteRenderer.flipX ? -1 : 1;
                effect.transform.localScale = new Vector3(direction, 1, 1);

                Debug.Log($"ChemChuong effect spawned at specific point facing {(spriteRenderer.flipX ? "LEFT" : "RIGHT")}!");
            }
        }
    }

    // Animation Event Method - Dedicated for Level 2 Ultimate spawn
    public void SpawnUltiLevel2()
    {
        if (UltiEffect != null)
        {
            Transform spawnPoint = attackPointUlti != null ? attackPointUlti : attackPoint;
            
            if (spawnPoint != null)
            {
                GameObject effect = Instantiate(UltiEffect, spawnPoint.position, Quaternion.identity);
                
                // Ensure correct direction for Level 2
                float direction = spriteRenderer.flipX ? -1 : 1;
                effect.transform.localScale = new Vector3(direction, 1, 1);
                
                Debug.Log("Level 2 Ultimate effect spawned from animator event!");
                
                // Optional: Add extra effects or functionality specific to Level 2
                // For example, you could add screen shake, camera effects, etc.
            }
            else
            {
                Debug.LogError("No attack point found for Ultimate effect spawn!");
            }
        }
        else
        {
            Debug.LogError("UltiEffect prefab is not assigned!");
        }
    }

    // Method để gọi từ Animation Event của Attack3 (chiêu chưởng) - Level 2
    public void SpawnChuongLevel2()
    {
        SpawnSkillEffect("Attack3");
        Debug.Log("Chuong effect spawned from Level 2 animator event!");
    }

    // Method để spawn Effect_Chuong với xử lý hướng tại attackPoint - Level 2
    public void SpawnChuongAtAttackPointLevel2()
    {
        if (Effect_Chuong != null && attackPoint != null)
        {
            // Spawn effect tại vị trí attackPoint (đã được lật theo hướng player)
            GameObject effect = Instantiate(Effect_Chuong, attackPoint.position, Quaternion.identity);
            
            // Lật scale của effect theo hướng player
            float direction = spriteRenderer.flipX ? -1 : 1;
            effect.transform.localScale = new Vector3(direction, 1, 1);
            
            Debug.Log($"Level 2 Chuong effect spawned at attack point facing {(spriteRenderer.flipX ? "LEFT" : "RIGHT")}!");
        }
        else
        {
            Debug.LogError("Effect_Chuong prefab or attackPoint is missing in Level 2!");
        }
    }

    // Method tổng hợp cho Level 2 - xử lý cả vị trí và hướng
    public void SpawnChuongWithDirectionLevel2()
    {
        if (Effect_Chuong != null && attackPoint != null)
        {
            // 1. AttackPoint đã được lật trong FlipTransforms()
            // 2. Spawn effect tại attackPoint
            GameObject effect = Instantiate(Effect_Chuong, attackPoint.position, Quaternion.identity);
            
            // 3. Lật scale effect theo hướng player
            if (spriteRenderer.flipX)
            {
                // Player quay trái
                effect.transform.localScale = new Vector3(-Mathf.Abs(effect.transform.localScale.x), 
                                                        effect.transform.localScale.y, 
                                                        effect.transform.localScale.z);
            }
            else
            {
                // Player quay phải
                effect.transform.localScale = new Vector3(Mathf.Abs(effect.transform.localScale.x), 
                                                        effect.transform.localScale.y, 
                                                        effect.transform.localScale.z);
            }
            
            Debug.Log($"Level 2 Chuong spawned with direction at ({attackPoint.position}) facing {(spriteRenderer.flipX ? "LEFT" : "RIGHT")}");
        }
    }

    // Boss Interaction Methods (giữ nguyên để tương thích)
    public void OnDamageDealt(int damageAmount)
    {
        AddRagePoints(rageGainPerHit);
        Debug.Log($"Player dealt {damageAmount} damage and gained {rageGainPerHit} rage points!");
    }

    public void OnDamageDealt(int damageAmount, int rageGain)
    {
        AddRagePoints(rageGain);
        Debug.Log($"Player dealt {damageAmount} damage and gained {rageGain} rage points!");
    }

    public void OnSuccessfulHit(int damageDealt)
    {
        AddRagePoints(rageGainPerSuccessfulHit);
        Debug.Log($"Player hit boss for {damageDealt} damage and gained {rageGainPerSuccessfulHit} rage points!");
    }

    // Hàm đơn giản để gọi từ Animation Event cho Ultimate Level 2
    public void SpawnUltiEffectLevel2()
    {
        if (UltiEffect != null)
        {
            Transform spawnPoint = attackPointUlti != null ? attackPointUlti : attackPoint;
            
            if (spawnPoint != null)
            {
                // Chỉ cần spawn effect - không cần xử lý rotation hay scale
                // UltiEffectLevel2 script sẽ tự xử lý hướng di chuyển
                GameObject effect = Instantiate(UltiEffect, spawnPoint.position, Quaternion.identity);
                
                Debug.Log("Ultimate Level 2 effect spawned - will move based on player direction");
            }
            else
            {
                Debug.LogError("No attack point found for Ultimate effect spawn!");
            }
        }
        else
        {
            Debug.LogError("UltiEffect prefab is not assigned!");
        }
    }
}