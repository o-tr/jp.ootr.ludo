using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace jp.ootr.ludo
{
    /// <summary>
    /// アタッチされた GameObject をローカルプレイヤーの頭の方向へ向け続ける
    /// Billboard コンポーネント。lobbyPanel / gamePanel / endPanel 等に付与して使用する。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LudoBillboard : UdonSharpBehaviour
    {
        /// <summary>Y 軸回転のみを行い、パネルを常に垂直に保つ場合は true。</summary>
        [SerializeField] private bool lockVertical = true;

        void Update()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer == null) return;

            Vector3 headPos = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            Vector3 dir = headPos - transform.position;

            if (lockVertical) dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(-dir);
        }
    }
}
