using System.Collections;
using System.Collections.Generic;
using Levels.Core;
using Levels.Player;
using Levels.Scenes;
using UnityEngine;

namespace Levels.Enemies
{
    [DisallowMultipleComponent]
    public sealed class SpiderBoss : MonoBehaviour
    {
        public enum EncounterState { Intro, Phase1, TransitionToPhase2, Phase2, TransitionToPhase3, Phase3, Defeated }

        [Header("Arena")]
        [SerializeField] private PlayerHealth player;
        [SerializeField] private EnemyHealth boss;
        [SerializeField] private Collider2D hurtbox;
        [SerializeField] private Transform visuals;
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private GameObject entranceSeal;
        [SerializeField] private SpriteRenderer shield;
        [SerializeField] private SpriteRenderer groundMarker;
        [SerializeField] private SpriteRenderer waveMarker;
        [SerializeField] private SpriteRenderer[] fanMarkers;
        [SerializeField] private SpriteRenderer projectileTemplate;
        [SerializeField] private SpriteRenderer webTemplate;
        [SerializeField] private GameObject healthBar;
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private Rect arenaBounds = new Rect(-8.5f, -4.75f, 17f, 9.5f);
        [SerializeField, Min(.1f)] private float landingInset = 1.45f;

        [Header("Intro and hit response")]
        [SerializeField, Min(.1f)] private float introWalkSeconds = .36f;
        [SerializeField, Min(.1f)] private float introDropSeconds = .55f;
        [SerializeField, Min(0f)] private float invulnerabilitySeconds = .7f;
        [SerializeField, Min(0f)] private float knockbackSeconds = .22f;
        [SerializeField, Min(0f)] private float victoryDelay = 1.1f;

        [Header("Phase 1")]
        [SerializeField, Min(1)] private int hitsForPhase2 = 15;
        [SerializeField, Min(.1f)] private float patrolHalfWidth = 5.9f;
        [SerializeField, Min(.1f)] private float patrolLegSeconds = 3f;
        [SerializeField, Min(.1f)] private float shotInterval = 2f;
        [SerializeField, Min(.1f)] private float meleeInterval = 1.8f;
        [SerializeField, Min(.05f)] private float meleeWarning = .5f;
        [SerializeField, Min(.1f)] private float meleeRadius = 2.05f;
        [SerializeField, Min(1)] private int meleeDamage = 3;

        [Header("Web attacks")]
        [SerializeField, Min(1)] private int projectileDamage = 2;
        [SerializeField, Min(.1f)] private float projectileSpeed = 5.8f;
        [SerializeField, Min(.05f)] private float projectileRadius = .25f;
        [SerializeField, Min(.1f)] private float projectileFollowThrough = 1.25f;
        [SerializeField, Range(1, 24)] private int maxProjectiles = 12;
        [SerializeField, Range(1, 8)] private int maxWebs = 8;
        [SerializeField, Min(.1f)] private float webLife = 8f;
        [SerializeField, Min(.1f)] private float webRadius = .66f;
        [SerializeField, Min(1)] private int webDamage = 1;
        [SerializeField, Range(0f, 1f)] private float webSpeedMultiplier = .6f;
        [SerializeField, Min(.1f)] private float webSlowSeconds = 1f;
        [SerializeField, Min(0f)] private float webSharedImmunity = .65f;

        [Header("Transitions")]
        [SerializeField, Min(.1f)] private float transitionSeconds = 3f;
        [SerializeField, Min(.05f)] private float waveSweepSeconds = .32f;
        [SerializeField, Min(.05f)] private float waveAftermath = .55f;
        [SerializeField, Min(1)] private int waveDamage = 6;
        [SerializeField, Min(0f)] private float waveKnockback = 12f;

        [Header("Phase 2")]
        [SerializeField, Min(.1f)] private float shadowTrackingSeconds = 2f;
        [SerializeField, Min(.1f)] private float shadowLockedSeconds = .8f;
        [SerializeField, Min(.1f)] private float landingRadius = 1.8f;
        [SerializeField, Min(1)] private int landingDamage = 4;
        [SerializeField, Min(0f)] private float landingKnockback = 9f;
        [SerializeField, Min(.1f)] private float phase2Stun = 2.5f;

        [Header("Spin")]
        [SerializeField, Min(.05f)] private float spinWarning = .7f;
        [SerializeField, Min(.1f)] private float spinSeconds = .8f;
        [SerializeField, Min(.1f)] private float spinRadius = 2.28f;
        [SerializeField, Min(1)] private int spinDamage = 4;
        [SerializeField, Min(0f)] private float spinKnockback = 8f;

        [Header("Phase 3")]
        [SerializeField, Min(.1f)] private float jumpSeconds = 1.2f;
        [SerializeField, Min(.1f)] private float minimumJumpDistance = 3f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private AnimationCurve jumpTravel = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 1.65f), new Keyframe(.5f, .5f, .35f, .35f),
            new Keyframe(1f, 1f, 1.65f, 0f));
        [SerializeField, Min(.05f)] private float fanWarning = .65f;
        [SerializeField, Range(5f, 50f)] private float fanAngle = 23f;
        [SerializeField, Min(.1f)] private float fanRecovery = .42f;
        [SerializeField, Min(1)] private int landingsBeforeStun = 3;
        [SerializeField, Min(.1f)] private float longSpinSeconds = 1.65f;
        [SerializeField, Min(.1f)] private float phase3Stun = 3f;
        [SerializeField, Min(1f)] private float enrageSpeed = 1.25f;
        [SerializeField, Range(0f, 1f)] private float combinedAttackHealthThreshold = .18f;

        private static readonly Color Amber = new Color(1f, .58f, .13f, .36f);
        private static readonly Color Red = new Color(1f, .22f, .13f, .50f);
        private static readonly Color Shadow = new Color(.03f, .02f, .035f, .62f);
        private static readonly Color StunnedTint = new Color(.42f, .72f, 1f, .23f);

        private sealed class Projectile
        {
            public SpriteRenderer Visual;
            public Vector2 Direction;
            public float DistanceLeft;
        }

        private sealed class Web
        {
            public SpriteRenderer Visual;
            public float ExpiresAt;
            public bool Occupied;
        }

        private readonly List<Projectile> projectiles = new();
        private readonly List<Web> webs = new();
        private readonly RaycastHit2D[] projectileHit = new RaycastHit2D[1];
        private ContactFilter2D projectileFilter;
        private PlayerMovement movement;
        private PlayerAttack playerAttack;
        private Collider2D playerCollider;
        private Coroutine sequence;
        private bool controlsLocked;
        private bool vulnerable;
        private int phase1Hits;
        private float nextWebDamage;
        private Vector3 cameraCenter;
        private float shakeTime;
        private float shakeStrength;
        private Vector3 patrolCenter;

        public EncounterState State { get; private set; }
        private int Phase2Threshold => (int)(boss.MaxHealth * 2L / 3);
        private int Phase3Threshold => boss.MaxHealth / 3;
        private bool Enraged => boss.CurrentHealth <= boss.MaxHealth * .10f;
        private bool CombinedAttackReady => boss.CurrentHealth <= boss.MaxHealth * combinedAttackHealthThreshold;
        private Vector2 BossPosition => boss.transform.position;
        private Vector2 PlayerPosition => playerCollider.bounds.center;

        private void Awake()
        {
            movement = player.GetComponent<PlayerMovement>();
            playerAttack = player.GetComponent<PlayerAttack>();
            playerCollider = player.GetComponent<Collider2D>();
            projectileFilter.SetLayerMask(1 << player.gameObject.layer);
            projectileFilter.useTriggers = false;
            patrolCenter = boss.transform.position;
            cameraCenter = arenaCamera.transform.position;
            boss.DamageLimit = LimitDamage;
            boss.DamageApplied += BossHit;
            boss.HealthChanged += UpdateHealthBar;
            boss.Defeated += Defeat;
            player.HealthChanged += PlayerHealthChanged;
            controlsLocked = true;
            movement.SetScriptedInput(Vector2.zero);
            playerAttack.enabled = false;
            SetTangible(false);
            visuals.gameObject.SetActive(false);
            healthBar.SetActive(false);
        }

        private void Start()
        {
            if (boss.MaxHealth < 3) boss.SetInitialMaxHealth(3);
            sequence = StartCoroutine(Intro());
        }

        private void Update()
        {
            if (!GameFlow.IsGameplayActive || player == null) return;
            UpdateShake();
            MoveProjectiles();
            if (GameFlow.IsGameplayActive) UpdateWebs();
        }

        private IEnumerator Intro()
        {
            while (!GameFlow.IsGameplayActive) yield return null;
            movement.SetScriptedInput(Vector2.up);
            yield return WaitInFight(introWalkSeconds);
            movement.SetScriptedInput(Vector2.zero);
            entranceSeal.SetActive(true);

            float dropHeight = HeightAboveView();
            visuals.localPosition = Vector3.up * dropHeight;
            visuals.gameObject.SetActive(true);
            Marker(groundMarker, BossPosition, landingRadius, Shadow);
            for (float time = 0; time < introDropSeconds; time += Time.deltaTime)
            {
                float progress = time / introDropSeconds;
                visuals.localPosition = Vector3.up * (dropHeight * (1f - progress * progress));
                yield return null;
            }
            ResetVisuals();
            Shake(.27f, .18f);
            yield return WaitInFight(.28f);
            healthBar.SetActive(true);
            ReleaseControls();
            State = EncounterState.Phase1;
            yield return Hunt();
        }

        private IEnumerator Hunt()
        {
            Rect bounds = LandingBounds();
            float left = Mathf.Max(bounds.xMin, patrolCenter.x - patrolHalfWidth);
            float right = Mathf.Min(bounds.xMax, patrolCenter.x + patrolHalfWidth);
            float destination = right;
            float patrolSpeed = (right - left) / patrolLegSeconds;
            float nextShot = Time.time + shotInterval;
            float nextMelee = Time.time;
            SetTangible(true);
            vulnerable = true;

            while (true)
            {
                if (!GameFlow.IsGameplayActive) { yield return null; continue; }
                if (Touches(BossPosition, meleeRadius))
                {
                    if (Time.time >= nextMelee)
                    {
                        nextMelee = Time.time + meleeInterval;
                        Marker(groundMarker, BossPosition, meleeRadius, Amber);
                        yield return WaitInFight(meleeWarning);
                        groundMarker.color = Red;
                        HitCircle(BossPosition, meleeRadius, meleeDamage, 0f);
                        yield return WaitInFight(.16f);
                        groundMarker.enabled = false;
                    }
                }
                else
                {
                    if (Time.time >= nextShot)
                    {
                        nextShot = Time.time + shotInterval;
                        ShootWeb(AimAtPlayer(), Vector2.Distance(BossPosition, PlayerPosition) + projectileFollowThrough);
                    }
                }

                Vector3 position = boss.transform.position;
                position.x = Mathf.MoveTowards(position.x, destination, patrolSpeed * Time.deltaTime);
                boss.transform.position = position;
                if (position.x == destination) destination = destination == right ? left : right;
                yield return null;
            }
        }

        private int LimitDamage(int amount)
        {
            if (!vulnerable) return 0;
            int floor = State == EncounterState.Phase1 ? Phase2Threshold :
                State == EncounterState.Phase2 ? Phase3Threshold : 0;
            return Mathf.Min(amount, boss.CurrentHealth - floor);
        }

        private void BossHit(int amount)
        {
            if (State == EncounterState.Phase1)
            {
                phase1Hits++;
                if (phase1Hits >= hitsForPhase2 || boss.CurrentHealth <= Phase2Threshold)
                    BeginTransition(true);
            }
            else if (State == EncounterState.Phase2 && boss.CurrentHealth <= Phase3Threshold)
                BeginTransition(false);
        }

        private void BeginTransition(bool toPhase2)
        {
            vulnerable = false;
            State = toPhase2 ? EncounterState.TransitionToPhase2 : EncounterState.TransitionToPhase3;
            StopCoroutine(sequence);
            ResetVisuals();
            ClearProjectiles();
            sequence = StartCoroutine(Transition(toPhase2));
        }

        private IEnumerator Transition(bool toPhase2)
        {
            shield.enabled = true;
            float waveEdge = Mathf.Lerp(arenaBounds.yMin, arenaBounds.yMax, 1f / 3f);
            if (toPhase2) ShowWave(waveEdge, Amber);
            Vector3 cameraStart = cameraCenter;
            for (float time = 0; time < transitionSeconds; time += Time.deltaTime)
            {
                float intensity = .07f + .09f * (time / transitionSeconds);
                visuals.localPosition = new Vector3(Mathf.Sin(time * 69f) * intensity, Mathf.Cos(time * 57f) * intensity);
                visuals.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 51f) * 6f);
                arenaCamera.transform.position = cameraStart + (Vector3)(intensity * .45f *
                    new Vector2(Mathf.Sin(time * 45f), Mathf.Cos(time * 51f)));
                yield return null;
            }

            while (GameFlow.State == RunState.Paused) yield return null;
            ResetVisuals();
            if (toPhase2)
            {
                shield.enabled = true;
                ClearWebs();
                yield return SweepWebWave(waveEdge);
                if (!GameFlow.IsGameplayActive) yield break;
                for (int row = 0; row < 2; row++)
                {
                    for (int column = 0; column < 4; column++)
                    {
                        Vector2 point = new Vector2(
                            Mathf.Lerp(arenaBounds.xMin, arenaBounds.xMax, (column + .5f + row * .22f) / 4f),
                            Mathf.Lerp(waveEdge, arenaBounds.yMax, (row + .5f) / 2f));
                        AddWeb(point, true);
                    }
                }
                Shake(.3f, .15f);
                yield return WaitInFight(waveAftermath);
                ClearWebs();
                movement.ClearSlow();
                ResetVisuals();
                State = EncounterState.Phase2;
                yield return LeapAndSpin();
            }
            else
            {
                Shake(.23f, .12f);
                State = EncounterState.Phase3;
                yield return Frenzy();
            }
        }

        private IEnumerator SweepWebWave(float edge)
        {
            bool contacted = false;
            float elapsed = 0f;
            while (true)
            {
                while (GameFlow.State == RunState.Paused) yield return null;
                float front = Mathf.Lerp(arenaBounds.yMax, edge, elapsed / waveSweepSeconds);
                ShowWave(front, Red);
                if (!contacted && playerCollider.bounds.max.y >= front)
                {
                    contacted = true;
                    HurtPlayer(waveDamage, waveKnockback, BossPosition);
                    if (!GameFlow.IsGameplayActive) yield break;
                }
                if (elapsed >= waveSweepSeconds) yield break;
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        private IEnumerator LeapAndSpin()
        {
            while (true)
            {
                vulnerable = false;
                SetTangible(false);
                float riseSeconds = Mathf.Min(.22f, shadowTrackingSeconds);
                float takeoffHeight = HeightAboveView();
                for (float time = 0; time < shadowTrackingSeconds; time += Time.deltaTime)
                {
                    if (time < riseSeconds) visuals.localPosition = Vector3.up * (takeoffHeight * time / riseSeconds);
                    else if (visuals.gameObject.activeSelf) visuals.gameObject.SetActive(false);
                    Marker(groundMarker, ValidLanding(PlayerPosition), landingRadius, Shadow);
                    yield return null;
                }

                visuals.gameObject.SetActive(false);
                Vector2 target = ValidLanding(PlayerPosition);
                Marker(groundMarker, target, landingRadius, Amber);
                float fallSeconds = Mathf.Min(.18f, shadowLockedSeconds * .45f);
                yield return WaitInFight(shadowLockedSeconds - fallSeconds);
                boss.transform.position = target;
                float fallHeight = HeightAboveView();
                visuals.localPosition = Vector3.up * fallHeight;
                visuals.gameObject.SetActive(true);
                for (float time = 0; time < fallSeconds; time += Time.deltaTime)
                {
                    float progress = time / fallSeconds;
                    visuals.localPosition = Vector3.up * (fallHeight * (1f - progress * progress));
                    yield return null;
                }
                while (GameFlow.State == RunState.Paused) yield return null;
                visuals.localPosition = Vector3.zero;
                groundMarker.color = Red;
                Shake(.24f, .17f);
                HitCircle(target, landingRadius, landingDamage, landingKnockback);
                if (!GameFlow.IsGameplayActive) yield break;
                yield return Stun(phase2Stun);
                yield return Spin(spinSeconds, false);
            }
        }

        private IEnumerator Frenzy()
        {
            int landings = 0;
            while (true)
            {
                SetTangible(false);
                Vector2 start = BossPosition;
                Vector2 target = ChooseLanding(start);
                float duration = jumpSeconds / (Enraged ? enrageSpeed : 1f);
                for (float time = 0; time < duration; time += Time.deltaTime)
                {
                    float progress = time / duration;
                    float height = Mathf.Sin(progress * Mathf.PI);
                    boss.transform.position = Vector2.Lerp(start, target, jumpTravel.Evaluate(progress));
                    visuals.localPosition = Vector3.up * (jumpHeight * height);
                    Marker(groundMarker, BossPosition, Mathf.Lerp(.83f, 1.37f, height), Shadow);
                    yield return null;
                }

                boss.transform.position = target;
                ResetVisuals();
                Shake(.13f, .075f);
                ++landings;
                if (landings >= landingsBeforeStun)
                {
                    yield return Spin(longSpinSeconds, Enraged);
                    landings = 0;
                    yield return Stun(phase3Stun);
                }
                else if (CombinedAttackReady || Random.value < .5f)
                    yield return Spin(spinSeconds, CombinedAttackReady);
                else
                    yield return Fan();
            }
        }

        private IEnumerator Stun(float duration)
        {
            SetTangible(true);
            vulnerable = true;
            Marker(groundMarker, BossPosition, 1.36f, StunnedTint);
            visuals.localRotation = Quaternion.Euler(0f, 0f, 12f);
            yield return WaitInFight(duration);
            vulnerable = false;
            ResetVisuals();
        }

        private IEnumerator Spin(float duration, bool withFan)
        {
            Vector2 position = BossPosition;
            Vector2 aim = AimAtPlayer();
            float reach = Vector2.Distance(position, PlayerPosition) + projectileFollowThrough;
            Marker(groundMarker, position, spinRadius, Amber);
            if (withFan) ShowFan(aim, reach);
            yield return WaitInFight(spinWarning);
            HideFan();
            if (withFan) FireFan(aim, reach);
            groundMarker.color = Red;
            bool contacted = false;
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                visuals.localRotation = Quaternion.Euler(0, 0, time * 940f);
                if (GameFlow.IsGameplayActive && !contacted && Touches(position, spinRadius))
                {
                    contacted = true;
                    HurtPlayer(spinDamage, spinKnockback, position);
                    if (!GameFlow.IsGameplayActive) yield break;
                }
                yield return null;
            }
            ResetVisuals();
        }

        private IEnumerator Fan()
        {
            Vector2 aim = AimAtPlayer();
            float reach = Vector2.Distance(BossPosition, PlayerPosition) + projectileFollowThrough;
            ShowFan(aim, reach);
            yield return WaitInFight(fanWarning);
            HideFan();
            FireFan(aim, reach);
            yield return WaitInFight(fanRecovery);
        }

        private static IEnumerator WaitInFight(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            while (GameFlow.State == RunState.Paused) yield return null;
        }

        private Vector2 AimAtPlayer()
        {
            Vector2 delta = PlayerPosition - BossPosition;
            return delta.sqrMagnitude > .001f ? delta.normalized : Vector2.down;
        }

        private Vector2 FanDirection(Vector2 aim, int index) =>
            Quaternion.Euler(0f, 0f, (index - 1) * fanAngle) * aim;

        private void ShowFan(Vector2 aim, float reach)
        {
            for (int i = 0; i < fanMarkers.Length; i++)
            {
                SpriteRenderer marker = fanMarkers[i];
                Vector2 direction = FanDirection(aim, i);
                float length = Mathf.Min(reach, DistanceToWall(BossPosition, direction));
                marker.transform.position = BossPosition + direction * (length * .5f);
                marker.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
                SizeSprite(marker, new Vector2(length, projectileRadius * 2f));
                marker.color = Amber;
                marker.enabled = true;
            }
        }

        private void HideFan()
        {
            foreach (SpriteRenderer marker in fanMarkers)
                if (marker != null) marker.enabled = false;
        }

        private void FireFan(Vector2 aim, float distance)
        {
            for (int i = 0; i < 3; i++) ShootWeb(FanDirection(aim, i), distance);
        }

        private float DistanceToWall(Vector2 start, Vector2 direction)
        {
            float x = Mathf.Abs(direction.x) < .0001f ? float.PositiveInfinity :
                ((direction.x > 0f ? arenaBounds.xMax : arenaBounds.xMin) - start.x) / direction.x;
            float y = Mathf.Abs(direction.y) < .0001f ? float.PositiveInfinity :
                ((direction.y > 0f ? arenaBounds.yMax : arenaBounds.yMin) - start.y) / direction.y;
            return Mathf.Min(x, y);
        }

        private void ShootWeb(Vector2 direction, float distance)
        {
            if (projectiles.Count >= maxProjectiles)
            {
                Destroy(projectiles[0].Visual.gameObject);
                projectiles.RemoveAt(0);
            }
            SpriteRenderer render = Instantiate(projectileTemplate, transform);
            render.transform.position = BossPosition;
            SizeSprite(render, Vector2.one * (projectileRadius * 2f));
            render.gameObject.SetActive(true);
            projectiles.Add(new Projectile { Visual = render, Direction = direction, DistanceLeft = distance });
        }

        private void MoveProjectiles()
        {
            if (Time.deltaTime <= 0f) return;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile shot = projectiles[i];
                float step = Mathf.Min(shot.DistanceLeft, projectileSpeed * Time.deltaTime);
                Vector2 start = shot.Visual.transform.position;
                float wall = DistanceToWall(start, shot.Direction);
                bool hitsWall = wall <= step;
                step = Mathf.Min(wall, step);
                Vector2 end = start + shot.Direction * step;
                bool hitsPlayer = Physics2D.CircleCast(start, projectileRadius, shot.Direction,
                    projectileFilter, projectileHit, step) > 0;
                shot.DistanceLeft -= step;
                if (hitsPlayer || hitsWall || shot.DistanceLeft <= 0f)
                {
                    Vector2 impact = hitsPlayer ? start + shot.Direction * projectileHit[0].distance : end;
                    Destroy(shot.Visual.gameObject);
                    projectiles.RemoveAt(i);
                    if (hitsPlayer) HurtPlayer(projectileDamage, 0f, start);
                    if (!GameFlow.IsGameplayActive) return;
                    if ((!hitsWall || hitsPlayer) && arenaBounds.Contains(impact)) AddWeb(impact);
                }
                else shot.Visual.transform.position = end;
            }
        }

        private void AddWeb(Vector2 position, bool suppressInitialContact = false)
        {
            if (webs.Count >= maxWebs)
            {
                Destroy(webs[0].Visual.gameObject);
                webs.RemoveAt(0);
            }
            SpriteRenderer web = Instantiate(webTemplate, transform);
            web.transform.position = position;
            SizeSprite(web, Vector2.one * (webRadius * 2f));
            web.gameObject.SetActive(true);
            webs.Add(new Web
            {
                Visual = web,
                ExpiresAt = Time.time + webLife,
                Occupied = suppressInitialContact && Touches(position, webRadius)
            });
        }

        private void UpdateWebs()
        {
            for (int i = webs.Count - 1; i >= 0; i--)
            {
                Web web = webs[i];
                if (Time.time >= web.ExpiresAt)
                {
                    Destroy(web.Visual.gameObject);
                    webs.RemoveAt(i);
                    continue;
                }
                bool inside = Touches(web.Visual.transform.position, webRadius);
                bool entered = inside && !web.Occupied;
                web.Occupied = inside;
                if (!entered) continue;
                movement.ApplySlow(webSpeedMultiplier, webSlowSeconds);
                if (Time.time < nextWebDamage) continue;
                nextWebDamage = Time.time + webSharedImmunity;
                HurtPlayer(webDamage, 0f, web.Visual.transform.position);
                if (!GameFlow.IsGameplayActive) return;
            }
        }

        private bool Touches(Vector2 center, float radius) =>
            (playerCollider.ClosestPoint(center) - center).sqrMagnitude <= radius * radius;

        private void HitCircle(Vector2 center, float radius, int damage, float knockback)
        {
            if (Touches(center, radius)) HurtPlayer(damage, knockback, center);
        }

        private void HurtPlayer(int damage, float knockback, Vector2 origin)
        {
            if (!player.TakeDamage(damage, invulnerabilitySeconds) || player.CurrentHealth == 0 || knockback <= 0f)
                return;
            Vector2 delta = PlayerPosition - origin;
            Vector2 direction = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.down;
            movement.ApplyKnockback(direction * knockback, knockbackSeconds);
        }

        private Rect LandingBounds()
        {
            float inset = Mathf.Min(landingInset, Mathf.Min(arenaBounds.width, arenaBounds.height) * .45f);
            return Rect.MinMaxRect(arenaBounds.xMin + inset, arenaBounds.yMin + inset,
                arenaBounds.xMax - inset, arenaBounds.yMax - inset);
        }

        private float HeightAboveView() => arenaCamera.transform.position.y + arenaCamera.orthographicSize +
            1.6f - BossPosition.y;

        private Vector2 ValidLanding(Vector2 point)
        {
            Rect area = LandingBounds();
            return new Vector2(Mathf.Clamp(point.x, area.xMin, area.xMax), Mathf.Clamp(point.y, area.yMin, area.yMax));
        }

        private Vector2 ChooseLanding(Vector2 start)
        {
            Rect area = LandingBounds();
            Vector2 farthest = new Vector2(start.x > area.center.x ? area.xMin : area.xMax,
                start.y > area.center.y ? area.yMin : area.yMax);
            float minimum = Mathf.Min(minimumJumpDistance, Vector2.Distance(start, farthest));
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 candidate = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
                if ((candidate - start).sqrMagnitude >= minimum * minimum) return candidate;
            }
            return farthest;
        }

        private void ShowWave(float edge, Color color)
        {
            waveMarker.transform.position = new Vector2(arenaBounds.center.x, (edge + arenaBounds.yMax) * .5f);
            SizeSprite(waveMarker, new Vector2(arenaBounds.width, arenaBounds.yMax - edge));
            waveMarker.color = color;
            waveMarker.enabled = true;
        }

        private static void Marker(SpriteRenderer marker, Vector2 position, float radius, Color color)
        {
            marker.transform.position = position;
            SizeSprite(marker, Vector2.one * (radius * 2f));
            marker.color = color;
            marker.enabled = true;
        }

        private static void SizeSprite(SpriteRenderer sprite, Vector2 size)
        {
            Vector3 source = sprite.sprite.bounds.size;
            sprite.transform.localScale = new Vector3(size.x / source.x, size.y / source.y, 1f);
        }

        private void SetTangible(bool enabled) => hurtbox.enabled = enabled;

        private void Shake(float duration, float strength)
        {
            shakeTime = duration;
            shakeStrength = strength;
        }

        private void UpdateShake()
        {
            if (shakeTime <= 0f) return;
            shakeTime = Mathf.Max(0f, shakeTime - Time.deltaTime);
            arenaCamera.transform.position = cameraCenter;
            if (shakeTime > 0f)
            {
                float t = Time.time;
                arenaCamera.transform.position += (Vector3)(shakeStrength * Mathf.Min(1f, shakeTime * 8f) *
                    new Vector2(Mathf.Sin(t * 115f), Mathf.Cos(t * 91f)));
            }
        }

        private void ResetVisuals()
        {
            if (groundMarker != null) groundMarker.enabled = false;
            if (waveMarker != null) waveMarker.enabled = false;
            if (shield != null) shield.enabled = false;
            HideFan();
            if (visuals != null)
            {
                visuals.localPosition = Vector3.zero;
                visuals.localRotation = Quaternion.identity;
            }
            shakeTime = 0f;
            if (arenaCamera != null) arenaCamera.transform.position = cameraCenter;
        }

        private void UpdateHealthBar(int current, int maximum)
        {
            Vector2 anchor = healthFill.anchorMax;
            anchor.x = (float)current / maximum;
            healthFill.anchorMax = anchor;
        }

        private void Defeat()
        {
            State = EncounterState.Defeated;
            StopEncounter();
            GameFlow.Instance.BossDefeated();
            sequence = StartCoroutine(ShowVictory());
        }

        private IEnumerator ShowVictory()
        {
            yield return new WaitForSeconds(victoryDelay);
            GameFlow flow = GameFlow.Instance;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (!flow.TryFinishVictory()) break;
                yield return new WaitWhile(() => SceneTransitionService.IsTransitioning);
                if (GameFlow.State != RunState.Victory) yield break;
            }
            Debug.LogError("The Win scene could not be loaded.", this);
        }

        private void PlayerHealthChanged(int current, int maximum)
        {
            if (current == 0) StopEncounter();
        }

        private void ReleaseControls()
        {
            if (!controlsLocked || movement == null) return;
            controlsLocked = false;
            movement.SetScriptedInput(null);
            if (GameFlow.IsGameplayActive && player != null && player.CurrentHealth > 0 && playerAttack != null)
                playerAttack.enabled = true;
        }

        private void ClearProjectiles()
        {
            foreach (Projectile shot in projectiles) Destroy(shot.Visual.gameObject);
            projectiles.Clear();
        }

        private void ClearWebs()
        {
            foreach (Web web in webs) Destroy(web.Visual.gameObject);
            webs.Clear();
        }

        private void StopEncounter()
        {
            StopAllCoroutines();
            vulnerable = false;
            ClearProjectiles();
            ClearWebs();
            if (healthBar != null) healthBar.SetActive(false);
            if (hurtbox != null) SetTangible(false);
            if (visuals != null) ResetVisuals();
            if (movement != null) movement.ClearMovementEffects();
            ReleaseControls();
        }

        private void OnDisable()
        {
            StopEncounter();
            if (boss != null)
            {
                boss.DamageLimit = null;
                boss.DamageApplied -= BossHit;
                boss.Defeated -= Defeat;
                boss.HealthChanged -= UpdateHealthBar;
            }
            if (player != null) player.HealthChanged -= PlayerHealthChanged;
        }
    }
}
