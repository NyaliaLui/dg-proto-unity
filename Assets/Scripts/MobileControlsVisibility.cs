using UnityEngine;
using UnityEngine.InputSystem;

namespace DgProto
{
    /// <summary>
    /// Hides the on-screen controls on platforms without a touchscreen so
    /// desktop dev mode stays clean. Editor preview can be forced on via
    /// <see cref="forceVisibleInEditor"/>.
    /// </summary>
    public class MobileControlsVisibility : MonoBehaviour
    {
        [Tooltip("When true, keep the Canvas visible in the Editor even if no touchscreen is present. Useful for previewing the layout with a mouse.")]
        [SerializeField] private bool forceVisibleInEditor = true;

        private void Awake()
        {
            bool show = Touchscreen.current != null
                     || Application.isMobilePlatform
                     || (forceVisibleInEditor && Application.isEditor);
            gameObject.SetActive(show);
        }
    }
}
