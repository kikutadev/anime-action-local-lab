using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class AnimeFighterMotor : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5.6f;
    [SerializeField] private float rotationSpeed = 14f;
    [SerializeField] private float dodgeSpeed = 11f;
    [SerializeField] private float gravity = 22f;
    [SerializeField] private float attackRange = 1.65f;
    [SerializeField] private float attackRadius = 0.75f;

    private CharacterController controller;
    private ProceduralAnimeRig rig;
    private Camera gameplayCamera;
    private float verticalVelocity;
    private float attackTimer;
    private float dodgeTimer;
    private Vector3 dodgeDirection;

    public float MoveAmount { get; private set; }
    public bool IsAttacking => attackTimer > 0f;
    public bool IsDodging => dodgeTimer > 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        rig = GetComponentInChildren<ProceduralAnimeRig>();
        gameplayCamera = Camera.main;
    }

    private void Update()
    {
        attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
        dodgeTimer = Mathf.Max(0f, dodgeTimer - Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0))
        {
            TryAttack();
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.K))
        {
            TryDodge();
        }

        Vector2 raw = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 move = CameraRelative(raw);
        MoveAmount = Mathf.Clamp01(raw.magnitude);

        if (IsDodging)
        {
            move = dodgeDirection * dodgeSpeed;
        }
        else
        {
            move *= moveSpeed;
            if (!IsAttacking && move.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(move.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            }
        }

        if (controller.isGrounded)
        {
            verticalVelocity = -1f;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);

        if (rig != null)
        {
            rig.SetState(MoveAmount, IsAttacking, IsDodging);
        }
    }

    private Vector3 CameraRelative(Vector2 input)
    {
        if (gameplayCamera == null)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        Vector3 forward = gameplayCamera.transform.forward;
        Vector3 right = gameplayCamera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        return (forward * input.y + right * input.x).normalized * Mathf.Clamp01(input.magnitude);
    }

    public void TriggerAttack() => TryAttack();

    private void TryAttack()
    {
        if (IsDodging || IsAttacking)
        {
            return;
        }

        float generatedDuration = rig != null ? rig.PlayAttack() : 0f;
        attackTimer = generatedDuration > 0f ? generatedDuration : 0.52f;
        Invoke(nameof(ResolveAttack), Mathf.Max(0.17f, attackTimer * 0.5f));
    }

    private void ResolveAttack()
    {
        Vector3 center = transform.position + transform.forward * attackRange + Vector3.up * 0.95f;
        Collider[] hits = Physics.OverlapSphere(center, attackRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit.transform.root == transform.root)
            {
                continue;
            }

            EnemyTarget target = hit.GetComponentInParent<EnemyTarget>();
            if (target != null)
            {
                target.Hit(transform.forward);
            }
        }
    }

    private void TryDodge()
    {
        if (IsAttacking || IsDodging)
        {
            return;
        }

        Vector2 raw = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 move = CameraRelative(raw);
        dodgeDirection = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
        dodgeTimer = 0.42f;
    }
}
