using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using jp.ootr.common;

namespace jp.ootr.ludo
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LudoGameController : BaseClass
    {
        // ─── Synced Fields ───────────────────────────────────────────────────
        [UdonSynced] private GamePhase   syncedGamePhase           = GamePhase.Idle;
        [UdonSynced] private int         syncedCurrentPlayerSlot;
        [UdonSynced] private int         syncedDiceValue;
        [UdonSynced] private int         syncedTurnSerial;
        [UdonSynced] private EndRuleMode syncedEndRuleMode         = EndRuleMode.FirstPlaceOnly;

        [UdonSynced] private int[]        syncedPlayerIds      = new int[4];
        [UdonSynced] private bool[]       syncedPlayerActive   = new bool[4];
        [UdonSynced] private bool[]       syncedPlayerFinished = new bool[4];
        [UdonSynced] private int[]        syncedPlayerRank     = new int[4];
        [UdonSynced] private int[]        syncedOrderRolls     = new int[4];

        [UdonSynced] private TokenState[] syncedTokenState    = new TokenState[16];
        [UdonSynced] private int[]        syncedTokenBoardPos = new int[16];
        [UdonSynced] private int[]        syncedTokenSteps    = new int[16];

        // ─── Local Mirror Fields ─────────────────────────────────────────────
        private GamePhase   _gamePhase;
        private int         _currentPlayerSlot;
        private int         _diceValue;
        private int         _turnSerial;
        private EndRuleMode _endRuleMode;

        private int[]        _playerIds      = new int[4];
        private bool[]       _playerActive   = new bool[4];
        private bool[]       _playerFinished = new bool[4];
        private int[]        _playerRank     = new int[4];
        private int[]        _orderRolls     = new int[4];

        private TokenState[] _tokenState    = new TokenState[16];
        private int[]        _tokenBoardPos = new int[16];
        private int[]        _tokenSteps    = new int[16];

        private int[]        _tokenOwnerSlot        = new int[16];
        private int[]        _legalMoveTokenIndices = new int[4];
        private int          _legalMoveCount;
        private bool         _isInitialized;
        private bool         _afterAnimationGameEnded = false;

        [SerializeField] private LudoBoardView    boardView;
        [SerializeField] private LudoUIController uiController;
        [SerializeField] private LudoSeatRequest[] seatRequests = new LudoSeatRequest[4];

        // ─── Constants ───────────────────────────────────────────────────────
        private readonly int[] START_POS    = { 0, 10, 20, 30 };
        private readonly int[] HOME_BASE    = { 40, 40, 40, 40 };
        private readonly int[] SAFE_SQUARES = { 0, 10, 20, 30 };

        private const int TOTAL_STEPS = 44;  // steps 0-39=Track(40 squares), 40-43=HomeRow(4 squares), 44=Home

        // ─── Lifecycle ───────────────────────────────────────────────────────
        void Start()
        {
            for (int i = 0; i < 16; i++)
            {
                _tokenOwnerSlot[i] = i / 4;
                _tokenState[i]     = TokenState.Yard;
                _tokenSteps[i]     = -1;
                _tokenBoardPos[i]  = -1;
            }
            _isInitialized = true;

            if (Networking.IsOwner(gameObject))
            {
                ConsoleLog("Owner starting Lobby");
                StartLobby();
            }
        }

        // ─── Deserialization ─────────────────────────────────────────────────
        public override void _OnDeserialization()
        {
            base._OnDeserialization();
            _gamePhase         = syncedGamePhase;
            _currentPlayerSlot = syncedCurrentPlayerSlot;
            _diceValue         = syncedDiceValue;
            _turnSerial        = syncedTurnSerial;
            _endRuleMode       = syncedEndRuleMode;

            for (int i = 0; i < 4; i++)
            {
                _playerIds[i]      = syncedPlayerIds[i];
                _playerActive[i]   = syncedPlayerActive[i];
                _playerFinished[i] = syncedPlayerFinished[i];
                _playerRank[i]     = syncedPlayerRank[i];
                _orderRolls[i]     = syncedOrderRolls[i];
            }

            for (int i = 0; i < 16; i++)
            {
                _tokenState[i]    = syncedTokenState[i];
                _tokenBoardPos[i] = syncedTokenBoardPos[i];
                _tokenSteps[i]    = syncedTokenSteps[i];
            }

            // Recompute legal moves on every client when in SelectMove phase so highlights are correct.
            if (_gamePhase == GamePhase.SelectMove)
                ComputeLegalMoves(_diceValue);
            else
                _legalMoveCount = 0;

            if (boardView != null)    boardView.OnStateUpdated(this);
            if (uiController != null) uiController.OnStateUpdated(this);
        }

        // ─── State Push ──────────────────────────────────────────────────────
        private void PushState()
        {
            _turnSerial++;
            syncedTurnSerial        = _turnSerial;
            syncedGamePhase         = _gamePhase;
            syncedCurrentPlayerSlot = _currentPlayerSlot;
            syncedDiceValue         = _diceValue;
            syncedEndRuleMode       = _endRuleMode;

            for (int i = 0; i < 4; i++)
            {
                syncedPlayerIds[i]      = _playerIds[i];
                syncedPlayerActive[i]   = _playerActive[i];
                syncedPlayerFinished[i] = _playerFinished[i];
                syncedPlayerRank[i]     = _playerRank[i];
                syncedOrderRolls[i]     = _orderRolls[i];
            }

            for (int i = 0; i < 16; i++)
            {
                syncedTokenState[i]    = _tokenState[i];
                syncedTokenBoardPos[i] = _tokenBoardPos[i];
                syncedTokenSteps[i]    = _tokenSteps[i];
            }

            Sync();
        }

        // ─── Board Math ──────────────────────────────────────────────────────
        private int ComputeBoardPos(int slot, int steps)
        {
            if (steps < 40)           return (START_POS[slot] + steps) % 40;
            if (steps < TOTAL_STEPS)  return HOME_BASE[slot] + (steps - 40);
            return -2;
        }

        /// <summary>View 層がステップ→盤面位置を変換するためのユーティリティ。</summary>
        public int ComputeTokenBoardPos(int tokenIdx, int steps)
        {
            if (steps < 0) return -1;           // Yard
            if (steps >= TOTAL_STEPS) return -2; // Home 完了
            return ComputeBoardPos(tokenIdx / 4, steps);
        }

        // ─── State Reset ─────────────────────────────────────────────────────
        private void ResetAllState()
        {
            for (int i = 0; i < 16; i++)
            {
                _tokenState[i]    = TokenState.Yard;
                _tokenSteps[i]    = -1;
                _tokenBoardPos[i] = -1;
            }
            for (int i = 0; i < 4; i++)
            {
                _playerIds[i]      = 0;
                _playerActive[i]   = false;
                _playerFinished[i] = false;
                _playerRank[i]     = 0;
                _orderRolls[i]     = 0;
            }
            _currentPlayerSlot = 0;
            _diceValue         = 0;
            _legalMoveCount    = 0;
            _gamePhase         = GamePhase.Lobby;
        }

        // ─── Game Flow ───────────────────────────────────────────────────────
        private void StartLobby()
        {
            _gamePhase = GamePhase.Lobby;
            PushState();
        }

        private void StartDetermineOrder()
        {
            _gamePhase = GamePhase.DetermineOrder;
            DetermineOrder();
        }

        private void DetermineOrder()
        {
            for (int i = 0; i < 4; i++)
                _orderRolls[i] = _playerActive[i] ? Random.Range(1, 7) : 0;

            while (true)
            {
                int maxRoll = 0;
                for (int i = 0; i < 4; i++)
                    if (_playerActive[i] && _orderRolls[i] > maxRoll)
                        maxRoll = _orderRolls[i];

                int tieCount = 0, winner = -1;
                for (int i = 0; i < 4; i++)
                    if (_playerActive[i] && _orderRolls[i] == maxRoll)
                    {
                        tieCount++;
                        winner = i;
                    }

                if (tieCount == 1)
                {
                    _currentPlayerSlot = winner;
                    break;
                }

                for (int i = 0; i < 4; i++)
                    _orderRolls[i] = (_playerActive[i] && _orderRolls[i] == maxRoll)
                        ? Random.Range(1, 7)
                        : 0;
            }

            _gamePhase = GamePhase.TurnBegin;
            PushState();
        }

        private void ValidateAndRoll()
        {
            _diceValue = Random.Range(1, 7);
            ComputeLegalMoves(_diceValue);

            if (_legalMoveCount == 0)
            {
                if (_gamePhase != GamePhase.GameEnd) AdvanceTurn();
                PushState();
            }
            else if (_legalMoveCount == 1)
            {
                ExecuteMove(_legalMoveTokenIndices[0]);
                _afterAnimationGameEnded = (_gamePhase == GamePhase.GameEnd);
                _gamePhase = GamePhase.ResolvingMove;
                PushState();
            }
            else
            {
                _gamePhase = GamePhase.SelectMove;
                PushState();
            }
        }

        private void HandleMoveRequest(int localIdx)
        {
            if (_gamePhase != GamePhase.SelectMove) return;

            int globalIdx = _currentPlayerSlot * 4 + localIdx;
            bool isLegal  = false;
            for (int i = 0; i < _legalMoveCount; i++)
                if (_legalMoveTokenIndices[i] == globalIdx) { isLegal = true; break; }
            if (!isLegal) return;

            ExecuteMove(globalIdx);
            _afterAnimationGameEnded = (_gamePhase == GamePhase.GameEnd);
            _gamePhase = GamePhase.ResolvingMove;
            PushState();
        }

        private void AdvanceTurn()
        {
            if (_diceValue == 6 && !_playerFinished[_currentPlayerSlot])
            {
                _gamePhase = GamePhase.TurnBegin;
            }
            else
            {
                _currentPlayerSlot = FindNextActiveSlot(_currentPlayerSlot);
                _gamePhase         = GamePhase.TurnBegin;
            }
        }

        // LudoBoardView から移動アニメーション完了時に呼ばれる（オーナーのみ処理）
        public void OnMoveAnimationComplete()
        {
            if (!Networking.IsOwner(gameObject)) return;
            if (_gamePhase != GamePhase.ResolvingMove) return;

            if (_afterAnimationGameEnded)
            {
                _gamePhase               = GamePhase.GameEnd;
                _afterAnimationGameEnded = false;
            }
            else
            {
                AdvanceTurn();
            }
            PushState();
        }

        // ─── Legal Move Logic ────────────────────────────────────────────────
        private void ComputeLegalMoves(int dice)
        {
            _legalMoveCount = 0;
            int baseIdx = _currentPlayerSlot * 4;
            for (int t = baseIdx; t < baseIdx + 4; t++)
                if (CanMoveToken(t, dice))
                    _legalMoveTokenIndices[_legalMoveCount++] = t;
        }

        private bool CanMoveToken(int tokenIdx, int dice)
        {
            TokenState state = _tokenState[tokenIdx];
            if (state == TokenState.Home) return false;

            int slot = tokenIdx / 4;

            if (state == TokenState.Yard)
                return dice == 6 && CanEnterStartSquare(slot);

            int fromSteps = _tokenSteps[tokenIdx];
            int toSteps   = fromSteps + dice;
            if (toSteps > TOTAL_STEPS) return false;
            if (!IsPathClear(slot, fromSteps, toSteps)) return false;
            if (toSteps == TOTAL_STEPS) return true;

            int destBoardPos = ComputeBoardPos(slot, toSteps);
            return CanLandAt(tokenIdx, destBoardPos);
        }

        private bool IsPathClear(int slot, int fromStep, int toStep)
        {
            for (int s = fromStep + 1; s < toStep; s++)
            {
                if (s >= 40) break;
                int boardPos = (START_POS[slot] + s) % 40;
                if (IsBlockAt(boardPos)) return false;
            }
            return true;
        }

        private bool IsBlockAt(int boardPos)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                int count = 0;
                for (int t = slot * 4; t < slot * 4 + 4; t++)
                    if (_tokenState[t] == TokenState.Track && _tokenBoardPos[t] == boardPos)
                        if (++count >= 2) return true;
            }
            return false;
        }

        private bool CanLandAt(int tokenIdx, int destBoardPos)
        {
            int ownerSlot = tokenIdx / 4;

            if (IsSafeSquare(destBoardPos))
            {
                for (int slot = 0; slot < 4; slot++)
                {
                    if (slot == ownerSlot) continue;
                    for (int t = slot * 4; t < slot * 4 + 4; t++)
                    {
                        if (_tokenState[t] != TokenState.Yard &&
                            _tokenState[t] != TokenState.Home &&
                            _tokenBoardPos[t] == destBoardPos) return false;
                    }
                }
            }

            if (destBoardPos < 40 && IsBlockAt(destBoardPos)) return false;

            return true;
        }

        private bool CanEnterStartSquare(int slot)
        {
            int startPos = START_POS[slot];

            for (int s = 0; s < 4; s++)
            {
                if (s == slot) continue;
                for (int t = s * 4; t < s * 4 + 4; t++)
                {
                    if (_tokenState[t] != TokenState.Yard &&
                        _tokenState[t] != TokenState.Home &&
                        _tokenBoardPos[t] == startPos) return false;
                }
            }

            int ownCount = 0;
            for (int t = slot * 4; t < slot * 4 + 4; t++)
                if (_tokenState[t] == TokenState.Track && _tokenBoardPos[t] == startPos)
                    if (++ownCount >= 2) return false;

            return true;
        }

        private bool IsSafeSquare(int boardPos)
        {
            for (int i = 0; i < SAFE_SQUARES.Length; i++)
                if (SAFE_SQUARES[i] == boardPos) return true;
            return false;
        }

        private int FindCapturedToken(int boardPos, int attackerSlot)
        {
            if (IsSafeSquare(boardPos)) return -1;
            for (int slot = 0; slot < 4; slot++)
            {
                if (slot == attackerSlot) continue;
                int count = 0, victim = -1;
                for (int t = slot * 4; t < slot * 4 + 4; t++)
                    if (_tokenState[t] == TokenState.Track && _tokenBoardPos[t] == boardPos)
                    {
                        count++;
                        victim = t;
                    }
                if (count == 1) return victim;
            }
            return -1;
        }

        // ─── Move Execution ──────────────────────────────────────────────────
        private void ExecuteMove(int globalTokenIdx)
        {
            int slot = globalTokenIdx / 4;

            if (_tokenState[globalTokenIdx] == TokenState.Yard)
            {
                _tokenSteps[globalTokenIdx]    = 0;
                _tokenState[globalTokenIdx]    = TokenState.Track;
                _tokenBoardPos[globalTokenIdx] = START_POS[slot];
                return;
            }

            int newSteps = _tokenSteps[globalTokenIdx] + _diceValue;
            _tokenSteps[globalTokenIdx] = newSteps;

            if (newSteps >= TOTAL_STEPS)
            {
                _tokenState[globalTokenIdx]    = TokenState.Home;
                _tokenBoardPos[globalTokenIdx] = -2;
                CheckPlayerFinished(slot);
            }
            else if (newSteps >= 40)
            {
                _tokenState[globalTokenIdx]    = TokenState.HomeRow;
                _tokenBoardPos[globalTokenIdx] = ComputeBoardPos(slot, newSteps);
            }
            else
            {
                _tokenState[globalTokenIdx]    = TokenState.Track;
                _tokenBoardPos[globalTokenIdx] = ComputeBoardPos(slot, newSteps);
                int captured = FindCapturedToken(_tokenBoardPos[globalTokenIdx], slot);
                if (captured != -1)
                {
                    _tokenState[captured]    = TokenState.Yard;
                    _tokenBoardPos[captured] = -1;
                    _tokenSteps[captured]    = -1;
                }
            }
        }

        private void CheckPlayerFinished(int slot)
        {
            for (int t = slot * 4; t < slot * 4 + 4; t++)
                if (_tokenState[t] != TokenState.Home) return;

            _playerRank[slot]     = CountFinishedPlayers() + 1;
            _playerFinished[slot] = true;
            CheckGameEnd();
        }

        private void CheckGameEnd()
        {
            if (_endRuleMode == EndRuleMode.FirstPlaceOnly)
            {
                for (int s = 0; s < 4; s++)
                    if (_playerFinished[s]) { _gamePhase = GamePhase.GameEnd; return; }
            }
            else
            {
                int unfinished = 0;
                for (int s = 0; s < 4; s++)
                    if (_playerActive[s] && !_playerFinished[s]) unfinished++;

                if (unfinished <= 1)
                {
                    for (int s = 0; s < 4; s++)
                    {
                        if (_playerActive[s] && !_playerFinished[s])
                        {
                            _playerRank[s]     = CountFinishedPlayers() + 1;
                            _playerFinished[s] = true;
                        }
                    }
                    _gamePhase = GamePhase.GameEnd;
                }
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────
        private int CountFinishedPlayers()
        {
            int n = 0;
            for (int i = 0; i < 4; i++)
                if (_playerFinished[i]) n++;
            return n;
        }

        public int CountActivePlayers()
        {
            int n = 0;
            for (int i = 0; i < 4; i++)
                if (_playerActive[i]) n++;
            return n;
        }

        private int FindNextActiveSlot(int fromSlot)
        {
            for (int i = 1; i <= 4; i++)
            {
                int s = (fromSlot + i) % 4;
                if (_playerActive[s] && !_playerFinished[s]) return s;
            }
            return fromSlot;
        }

        private int GetSlotByPlayerId(int playerId)
        {
            for (int i = 0; i < 4; i++)
                if (_playerIds[i] == playerId) return i;
            return -1;
        }

        public int GetLocalPlayerSlot()
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (lp == null) return -1;
            return GetSlotByPlayerId(lp.playerId);
        }

        private void StartGameEndFromLeave()
        {
            int rank = CountFinishedPlayers() + 1;
            for (int s = 0; s < 4; s++)
            {
                if (_playerActive[s] && !_playerFinished[s])
                {
                    _playerRank[s]     = rank++;
                    _playerFinished[s] = true;
                }
            }
            _gamePhase = GamePhase.GameEnd;
            PushState();
        }

        // ─── Network Events ──────────────────────────────────────────────────
        public void RequestJoinSlot0() => HandleJoin(0);
        public void RequestJoinSlot1() => HandleJoin(1);
        public void RequestJoinSlot2() => HandleJoin(2);
        public void RequestJoinSlot3() => HandleJoin(3);

        private void HandleJoin(int slot)
        {
            ConsoleLog($"HandleJoin called: slot={slot}, phase={_gamePhase}");
            if (slot < 0 || slot >= seatRequests.Length || seatRequests[slot] == null)
            {
                ConsoleWarn($"SeatRequest not configured for slot={slot}");
                return;
            }
            seatRequests[slot].Request();
        }

        public void OnSeatJoinRequest(int slot, int requesterPlayerId)
        {
            ConsoleLog($"OnSeatJoinRequest: slot={slot}, requesterPlayerId={requesterPlayerId}");
            if (!Networking.IsOwner(gameObject)) return;
            if (_gamePhase != GamePhase.Lobby) { ConsoleWarn("Wrong phase"); return; }
            if (_playerActive[slot]) { ConsoleWarn("Already active"); return; }
            if (GetSlotByPlayerId(requesterPlayerId) != -1) { ConsoleWarn("Player already joined"); return; }

            _playerIds[slot]    = requesterPlayerId;
            _playerActive[slot] = true;
            ConsoleLog($"Joined: slot={slot}, playerId={requesterPlayerId}");
            PushState();

            seatRequests[slot].ClearRequest();
        }

        public void RequestLeaveSlot0() => HandleLeave(0);
        public void RequestLeaveSlot1() => HandleLeave(1);
        public void RequestLeaveSlot2() => HandleLeave(2);
        public void RequestLeaveSlot3() => HandleLeave(3);

        private void HandleLeave(int slot)
        {
            if (!Networking.IsOwner(gameObject))
            {
                switch (slot)
                {
                    case 0: SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestLeaveSlot0)); break;
                    case 1: SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestLeaveSlot1)); break;
                    case 2: SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestLeaveSlot2)); break;
                    case 3: SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestLeaveSlot3)); break;
                }
                return;
            }

            if (_gamePhase != GamePhase.Lobby) return;
            // No sender-identity check possible via SendCustomNetworkEvent.
            // UI only shows leave button for the local player's own slot, so we trust the slot index.
            if (!_playerActive[slot]) return;

            _playerIds[slot]    = 0;
            _playerActive[slot] = false;
            PushState();
        }

        public void RequestRoll()
        {
            if (!Networking.IsOwner(gameObject))
            {
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestRoll));
                return;
            }
            if (_gamePhase != GamePhase.TurnBegin) return;
            ValidateAndRoll();
        }

        public void RequestMoveToken0() => HandleMoveRequestRelay(0);
        public void RequestMoveToken1() => HandleMoveRequestRelay(1);
        public void RequestMoveToken2() => HandleMoveRequestRelay(2);
        public void RequestMoveToken3() => HandleMoveRequestRelay(3);

        private void HandleMoveRequestRelay(int localIdx)
        {
            if (!Networking.IsOwner(gameObject))
            {
                switch (localIdx)
                {
                    case 0: SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestMoveToken0)); break;
                    case 1: SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestMoveToken1)); break;
                    case 2: SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestMoveToken2)); break;
                    case 3: SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestMoveToken3)); break;
                }
                return;
            }
            HandleMoveRequest(localIdx);
        }

        public void RequestStartGame()
        {
            if (!Networking.IsOwner(gameObject) || !Networking.IsMaster) return;
            if (CountActivePlayers() < 2) return;
            StartDetermineOrder();
        }

        public void RequestResetGame()
        {
            if (!Networking.IsOwner(gameObject) || !Networking.IsMaster) return;
            ResetAllState();
            PushState();
        }

        // ─── VRChat Callbacks ────────────────────────────────────────────────
        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            base.OnPlayerJoined(player);
            if (!Networking.IsOwner(gameObject)) return;
            if (player.isLocal) return;
            if (_gamePhase == GamePhase.Idle) return;
            RequestSerialization();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            base.OnPlayerLeft(player);
            if (!Networking.IsOwner(gameObject)) return;

            int slot = GetSlotByPlayerId(player.playerId);
            if (slot == -1) return;

            _playerIds[slot]    = 0;
            _playerActive[slot] = false;

            if (_gamePhase == GamePhase.Lobby) { PushState(); return; }

            if (_gamePhase == GamePhase.DetermineOrder)
            {
                if (CountActivePlayers() < 2) { StartGameEndFromLeave(); return; }
                StartDetermineOrder();
                return;
            }

            for (int t = slot * 4; t < slot * 4 + 4; t++)
            {
                _tokenState[t]    = TokenState.Yard;
                _tokenBoardPos[t] = -1;
                _tokenSteps[t]    = -1;
            }

            if (CountActivePlayers() < 2) { StartGameEndFromLeave(); return; }

            if (_currentPlayerSlot == slot &&
                (_gamePhase == GamePhase.TurnBegin ||
                 _gamePhase == GamePhase.SelectMove ||
                 _gamePhase == GamePhase.ResolvingMove))
            {
                _afterAnimationGameEnded = false;
                _currentPlayerSlot = FindNextActiveSlot(slot);
                _gamePhase         = GamePhase.TurnBegin;
            }

            CheckGameEnd();
            PushState();
        }

        public override void OnOwnershipTransferred(VRCPlayerApi newOwner)
        {
            base.OnOwnershipTransferred(newOwner);
            if (!Networking.IsOwner(gameObject)) return;

            _OnDeserialization();

            if (_gamePhase == GamePhase.GameEnd      ||
                _gamePhase == GamePhase.Idle          ||
                _gamePhase == GamePhase.Lobby         ||
                _gamePhase == GamePhase.DetermineOrder) return;

            // アニメーション待機中にオーナーが変わった場合：ゲーム終了チェック後にターン進行
            if (_gamePhase == GamePhase.ResolvingMove)
            {
                _afterAnimationGameEnded = false;
                CheckGameEnd();
                if (_gamePhase != GamePhase.GameEnd)
                    AdvanceTurn();
                PushState();
                return;
            }

            if (!_playerActive[_currentPlayerSlot])
            {
                _playerIds[_currentPlayerSlot]    = 0;
                _playerActive[_currentPlayerSlot] = false;
                _currentPlayerSlot = FindNextActiveSlot(_currentPlayerSlot);
                _gamePhase         = GamePhase.TurnBegin;

                if (CountActivePlayers() < 2) StartGameEndFromLeave();
                else PushState();
            }
        }

        // ─── Public API Surface ──────────────────────────────────────────────
        public GamePhase  GetPhase()               => _gamePhase;
        public int        GetCurrentSlot()         => _currentPlayerSlot;
        public int        GetDiceValue()           => _diceValue;
        public int        GetTurnSerial()          => _turnSerial;
        public bool       IsPlayerActive(int slot) => _playerActive[slot];
        public int        GetPlayerId(int slot)    => _playerIds[slot];
        public int        GetPlayerRank(int slot)  => _playerRank[slot];
        public bool       IsPlayerFinished(int s)  => _playerFinished[s];
        public int        GetOrderRoll(int slot)   => _orderRolls[slot];
        public TokenState GetTokenState(int i)     => _tokenState[i];
        public int        GetTokenBoardPos(int i)  => _tokenBoardPos[i];
        public int        GetTokenSteps(int i)     => _tokenSteps[i];
        public int[]      GetLegalMoves()          => _legalMoveTokenIndices;
        public int        GetLegalMoveCount()      => _legalMoveCount;

        // ─── Input Forwarders (called by LudoUIController) ───────────────────
        public void OnJoinSlotPressed(int slot)
        {
            switch (slot)
            {
                case 0: RequestJoinSlot0(); break;
                case 1: RequestJoinSlot1(); break;
                case 2: RequestJoinSlot2(); break;
                case 3: RequestJoinSlot3(); break;
            }
        }

        public void OnLeaveSlotPressed(int slot)
        {
            switch (slot)
            {
                case 0: RequestLeaveSlot0(); break;
                case 1: RequestLeaveSlot1(); break;
                case 2: RequestLeaveSlot2(); break;
                case 3: RequestLeaveSlot3(); break;
            }
        }

        public void OnRollPressed()             => RequestRoll();
        public void OnTokenPressed(int localIdx) => HandleMoveRequestRelay(localIdx);
        public void OnStartGamePressed()        => RequestStartGame();
        public void OnResetGamePressed()        => RequestResetGame();
    }
}
