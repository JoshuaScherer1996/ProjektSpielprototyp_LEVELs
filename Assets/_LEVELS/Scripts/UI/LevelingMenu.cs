using Levels.Core;
using Levels.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Levels.UI
{
    [DisallowMultipleComponent]
    public sealed class LevelingMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text details;
        [SerializeField] private UnityEngine.UI.Text feedback;
        [SerializeField] private UnityEngine.UI.Button playerButton;
        [SerializeField] private UnityEngine.UI.Button enemyButton;
        [SerializeField] private UnityEngine.UI.Button closeButton;
        [SerializeField] private InputSystemUIInputModule inputModule;

        private GameSession session;
        private PlayerMovement movement;
        private PlayerAttack attack;
        private bool restoreMovement;
        private bool restoreAttack;
        private int openedFrame;
        private InputAction cancelAction;
        private InputAction moveAction;
        private bool cancelRequested;
        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            panel.SetActive(false);
            inputModule.enabled = false;
        }

        private void OnEnable()
        {
            session = GameSession.Instance;
            session.Changed += Refresh;
            playerButton.onClick.AddListener(BuyPlayerLevel);
            enemyButton.onClick.AddListener(BuyEnemyLevel);
            closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            Close();
            if (session != null) session.Changed -= Refresh;
            playerButton.onClick.RemoveListener(BuyPlayerLevel);
            enemyButton.onClick.RemoveListener(BuyEnemyLevel);
            closeButton.onClick.RemoveListener(Close);
        }

        private void Update()
        {
            if (!IsOpen || Time.frameCount <= openedFrame) return;
            if (movement == null || cancelRequested)
            {
                cancelRequested = false;
                Close();
                return;
            }

            ClearSelectionOnMouseMove();
        }

        public bool Open(PlayerMovement player)
        {
            if (IsOpen || player == null || !player.enabled) return false;
            movement = player;
            attack = player.GetComponent<PlayerAttack>();
            restoreMovement = movement.enabled;
            restoreAttack = attack != null && attack.enabled;
            movement.enabled = false;
            if (attack != null) attack.enabled = false;
            openedFrame = Time.frameCount;
            panel.SetActive(true);
            inputModule.enabled = true;
            cancelAction = inputModule.cancel.action;
            if (cancelAction != null) cancelAction.performed += OnCancel;
            moveAction = inputModule.move.action;
            if (moveAction != null) moveAction.performed += OnMove;
            cancelRequested = false;
            feedback.text = "Choose one upgrade. Closing does not spend EXP.";
            Refresh();
            EventSystem.current.SetSelectedGameObject(playerButton.gameObject);
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            // A/ Cross is both UI Submit and Interact: do not reopen the station with the closing press.
            PlayerInputReader.ConsumeInteractForCurrentFrame();
            if (cancelAction != null) cancelAction.performed -= OnCancel;
            if (moveAction != null) moveAction.performed -= OnMove;
            cancelAction = null;
            moveAction = null;
            panel.SetActive(false);
            inputModule.enabled = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            // Keep the PlayerInput map/device pairing alive, as in the M3.5 transition fix.
            if (movement != null) movement.enabled = restoreMovement;
            if (attack != null) attack.enabled = restoreAttack;
            movement = null;
            attack = null;
        }

        private void OnCancel(InputAction.CallbackContext context) => cancelRequested = true;

        private void OnMove(InputAction.CallbackContext context)
        {
            if (context.ReadValue<Vector2>().sqrMagnitude > 0.01f && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(playerButton.gameObject);
        }

        private void ClearSelectionOnMouseMove()
        {
            if (Mouse.current == null || Mouse.current.delta.ReadValue().sqrMagnitude < 0.01f) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void BuyPlayerLevel()
        {
            if (!IsOpen || Time.frameCount <= openedFrame) return;
            bool bought = movement.GetComponent<PlayerProgression>().TryLevelUp();
            feedback.text = bought ? "Player upgraded. Current HP unchanged." : "Not enough EXP for a Player level.";
        }

        public void BuyEnemyLevel()
        {
            if (!IsOpen || Time.frameCount <= openedFrame) return;
            bool bought = session.TryLevelUpEnemies();
            feedback.text = bought ? "Enemies upgraded. Applies when entering the Plateau." : "Not enough EXP for an Enemy level.";
        }

        private void Refresh()
        {
            details.text = $"Player Level: {session.PlayerLevel}     Enemy Level: {session.EnemyLevel}\n" +
                $"Available EXP: {session.CurrentExperience}     Total earned: {session.TotalExperience}\n\n" +
                $"Player upgrade: {session.NextPlayerLevelExperience} EXP\n" +
                $"Enemy upgrade: {session.NextEnemyLevelExperience} EXP";
        }
    }
}
