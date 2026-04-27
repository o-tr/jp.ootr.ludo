using UdonSharp;
using UnityEngine;

namespace jp.ootr.ludo
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LudoTokenInteract : UdonSharpBehaviour
    {
        [SerializeField] private Collider boxCollider;

        private LudoBoardView _boardView;
        private int _tokenIndex;

        public void Init(int index, LudoBoardView view)
        {
            _tokenIndex = index;
            _boardView  = view;
            if (boxCollider == null)
                boxCollider = GetComponent<BoxCollider>();
        }

        public void SetInteractable(bool interactable)
        {
            if (boxCollider != null)
                boxCollider.enabled = interactable;
        }

        public override void Interact()
        {
            base.Interact();
            if (_boardView == null) return;
            _boardView.OnTokenClicked(_tokenIndex);
        }
    }
}
