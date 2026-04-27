using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace jp.ootr.ludo
{
    /// <summary>
    /// ロビー参加リクエストの送信元プレイヤーを確実に特定するための中継オブジェクト。
    /// 各スロットに1つ配置し、プレイヤーはオーナーシップを取得して自分の playerId を書き込む。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LudoSeatRequest : UdonSharpBehaviour
    {
        [SerializeField] private LudoGameController controller;
        [SerializeField] private int seatIndex;

        [UdonSynced] private int _pendingPlayerId = 0;

        /// <summary>
        /// ローカルプレイヤーがこのスロットへの参加リクエストを送る。
        /// オーナーシップを取得して自分の playerId を書き込み同期する。
        /// コントローラーオーナー自身がリクエストした場合は OnDeserialization が
        /// 自分には届かないため、直接処理を呼ぶ。
        /// </summary>
        public void Request()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer == null) return;

            Networking.SetOwner(localPlayer, gameObject);
            _pendingPlayerId = localPlayer.playerId;
            RequestSerialization();

            // コントローラーオーナー自身がリクエストした場合、
            // OnDeserialization は自分では受け取れないため直接処理する
            if (controller != null && Networking.IsOwner(controller.gameObject))
                controller.OnSeatJoinRequest(seatIndex, _pendingPlayerId);
        }

        /// <summary>
        /// コントローラーオーナーが参加処理完了後にリクエストをクリアする。
        /// </summary>
        public void ClearRequest()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _pendingPlayerId = 0;
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
            if (_pendingPlayerId == 0) return;
            if (controller == null) return;
            // コントローラーオーナーのみが参加処理を行う
            if (!Networking.IsOwner(controller.gameObject)) return;
            controller.OnSeatJoinRequest(seatIndex, _pendingPlayerId);
        }
    }
}
