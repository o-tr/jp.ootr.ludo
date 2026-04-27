using UdonSharp;
using UnityEngine;

namespace jp.ootr.ludo
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LudoBoardView : UdonSharpBehaviour
    {
        [SerializeField] private LudoGameController controller;

        [SerializeField] private Transform[] tokenObjects    = new Transform[16];
        [SerializeField] private Transform[] boardAnchors    = new Transform[40];
        [SerializeField] private Transform[] homeRowAnchors  = new Transform[16];
        [SerializeField] private Transform[] yardAnchors     = new Transform[16];

        [SerializeField] private GameObject[] highlights    = new GameObject[16];
        [SerializeField] private Vector3[]   visualOffsets = new Vector3[4];

        private LudoTokenInteract[] _tokenInteracts = new LudoTokenInteract[16];

        private int[]        viewTokenBoardPos = new int[16];
        private int[]        viewTokenSteps    = new int[16];
        private TokenState[] viewTokenState    = new TokenState[16];
        private int          viewTurnSerial    = -1;
        private bool         _isInitialized    = false;
        private int[]        _localVisualSlot  = new int[16];
        private Vector3[]    _targetPos        = new Vector3[16];
        private bool[]       _animating        = new bool[16];
        private int[]        _animTargetSteps  = new int[16];

        // 1マス移動あたりのアニメーション速度（Lerp係数）
        private const float STEP_ANIM_SPEED = 15f;

        void Start()
        {
            for (int i = 0; i < tokenObjects.Length; i++)
            {
                if (tokenObjects[i] == null) continue;
                var interact = tokenObjects[i].GetComponent<LudoTokenInteract>();
                if (interact == null) continue;
                interact.Init(i, this);
                _tokenInteracts[i] = interact;
            }
            _isInitialized = true;
            if (controller != null) SnapToCurrentState(controller);
        }

        public void OnStateUpdated(LudoGameController ctrl)
        {
            int serial = ctrl.GetTurnSerial();
            if (!_isInitialized || serial - viewTurnSerial > 1)
            {
                SnapToCurrentState(ctrl);
                return;
            }

            ComputeVisualSlots(ctrl);
            for (int i = 0; i < 16; i++)
            {
                int newSteps = ctrl.GetTokenSteps(i);
                if (newSteps == viewTokenSteps[i]) continue;

                if (newSteps > viewTokenSteps[i])
                {
                    // 前進移動：1マスずつ順に経由するアニメーション
                    _animTargetSteps[i] = newSteps;
                    _animating[i]       = true;
                    AdvanceToNextStep(i);
                }
                else
                {
                    // 捕獲・リセット（steps が減る）：直接スナップ
                    int newPos = ctrl.GetTokenBoardPos(i);
                    Transform anchor = GetAnchor(i, newPos);
                    if (anchor != null && tokenObjects[i] != null)
                        tokenObjects[i].position = anchor.position + GetVisualOffset(i, newPos);
                    _animating[i]        = false;
                    viewTokenSteps[i]    = newSteps;
                    viewTokenBoardPos[i] = newPos;
                }
            }
            UpdateHighlights(ctrl);
            viewTurnSerial = serial;
        }

        private void SnapToCurrentState(LudoGameController ctrl)
        {
            ComputeVisualSlots(ctrl);
            for (int i = 0; i < 16; i++)
            {
                int pos = ctrl.GetTokenBoardPos(i);
                Transform anchor = GetAnchor(i, pos);
                if (anchor != null && tokenObjects[i] != null)
                    tokenObjects[i].position = anchor.position + GetVisualOffset(i, pos);
                _animating[i]        = false;
                viewTokenBoardPos[i] = pos;
                viewTokenSteps[i]    = ctrl.GetTokenSteps(i);
                viewTokenState[i]    = ctrl.GetTokenState(i);
                _animTargetSteps[i]  = ctrl.GetTokenSteps(i);
            }
            viewTurnSerial = ctrl.GetTurnSerial();
            UpdateHighlights(ctrl);
        }

        /// <summary>
        /// viewTokenSteps[tokenIdx] の次のステップへのウェイポイントを設定する。
        /// Update() と OnStateUpdated() の両方から呼ばれる。
        /// </summary>
        private void AdvanceToNextStep(int tokenIdx)
        {
            int nextStep     = viewTokenSteps[tokenIdx] + 1;
            int nextBoardPos = controller.ComputeTokenBoardPos(tokenIdx, nextStep);
            Transform anchor = GetAnchor(tokenIdx, nextBoardPos);
            if (anchor == null)
            {
                // ホーム完了など、アンカーが存在しないマスに達した場合は終了
                _animating[tokenIdx] = false;
                return;
            }
            // 途中マスはオフセットなし、最終マスのみ重なりオフセットを適用
            Vector3 offset = (nextStep == _animTargetSteps[tokenIdx])
                ? GetVisualOffset(tokenIdx, nextBoardPos)
                : Vector3.zero;
            _targetPos[tokenIdx] = anchor.position + offset;
        }

        private void ComputeVisualSlots(LudoGameController ctrl)
        {
            int[] counts = new int[40];
            for (int i = 0; i < 16; i++) _localVisualSlot[i] = 0;
            for (int i = 0; i < 16; i++)
            {
                int pos = ctrl.GetTokenBoardPos(i);
                if (pos >= 0 && pos < 40)
                    _localVisualSlot[i] = counts[pos]++;
            }
        }

        private Vector3 GetVisualOffset(int tokenIdx, int boardPos)
        {
            if (boardPos < 0 || boardPos >= 40) return Vector3.zero;
            int slot = _localVisualSlot[tokenIdx];
            return (slot < visualOffsets.Length) ? visualOffsets[slot] : Vector3.zero;
        }

        private Transform GetAnchor(int tokenIdx, int boardPos)
        {
            if (boardPos == -1)
            {
                if (tokenIdx < yardAnchors.Length) return yardAnchors[tokenIdx];
                return null;
            }
            if (boardPos < 0) return null;
            if (boardPos >= 40 && boardPos < 44)
            {
                int colorSlot = tokenIdx / 4;
                int row       = boardPos - 40;
                int idx       = colorSlot * 4 + row;
                if (idx < homeRowAnchors.Length) return homeRowAnchors[idx];
                return null;
            }
            if (boardPos < boardAnchors.Length) return boardAnchors[boardPos];
            return null;
        }

        private void UpdateHighlights(LudoGameController ctrl)
        {
            bool isSelectMove = ctrl.GetPhase() == GamePhase.SelectMove;

            for (int i = 0; i < 16; i++)
            {
                if (highlights[i] != null) highlights[i].SetActive(false);

                if (_tokenInteracts[i] != null)
                {
                    bool isLocalTurn = isSelectMove && ctrl.GetLocalPlayerSlot() == ctrl.GetCurrentSlot();
                    _tokenInteracts[i].SetInteractable(isLocalTurn && IsTokenMoveable(ctrl, i));
                }
            }

            if (!isSelectMove) return;

            int count  = ctrl.GetLegalMoveCount();
            int[] moves = ctrl.GetLegalMoves();
            for (int i = 0; i < count; i++)
            {
                int idx = moves[i];
                if (idx >= 0 && idx < highlights.Length && highlights[idx] != null)
                {
                    Transform anchor = GetAnchor(idx, viewTokenBoardPos[idx]);
                    if (anchor != null)
                        highlights[idx].transform.position = anchor.position + GetVisualOffset(idx, viewTokenBoardPos[idx]);
                    highlights[idx].SetActive(true);
                }
            }
        }

        private bool IsTokenMoveable(LudoGameController ctrl, int tokenIdx)
        {
            int count = ctrl.GetLegalMoveCount();
            int[] moves = ctrl.GetLegalMoves();
            for (int i = 0; i < count; i++)
                if (moves[i] == tokenIdx) return true;
            return false;
        }

        public void OnTokenClicked(int tokenIdx)
        {
            if (controller == null) return;
            if (controller.GetPhase() != GamePhase.SelectMove) return;
            if (!IsTokenMoveable(controller, tokenIdx)) return;
            if (tokenIdx / 4 != controller.GetCurrentSlot()) return;
            if (controller.GetLocalPlayerSlot() != controller.GetCurrentSlot()) return;

            int localSlot = tokenIdx % 4;
            controller.OnTokenPressed(localSlot);
        }

        void Update()
        {
            for (int i = 0; i < 16; i++)
            {
                if (!_animating[i]) continue;
                if (tokenObjects[i] == null) continue;

                tokenObjects[i].position = Vector3.Lerp(
                    tokenObjects[i].position,
                    _targetPos[i],
                    Time.deltaTime * STEP_ANIM_SPEED);

                if (Vector3.Distance(tokenObjects[i].position, _targetPos[i]) < 0.001f)
                {
                    tokenObjects[i].position = _targetPos[i];

                    // 1ステップ進める
                    viewTokenSteps[i]++;
                    viewTokenBoardPos[i] = controller != null
                        ? controller.ComputeTokenBoardPos(i, viewTokenSteps[i])
                        : viewTokenBoardPos[i];

                    if (viewTokenSteps[i] >= _animTargetSteps[i])
                    {
                        _animating[i] = false;
                    }
                    else
                    {
                        AdvanceToNextStep(i);
                    }
                }
            }
        }
    }
}
