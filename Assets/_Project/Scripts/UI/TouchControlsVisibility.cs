using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRidge.UI
{
    /// <summary>Muestra los controles táctiles solo en móvil o si hay pantalla táctil (y en el editor, para probar).</summary>
    public class TouchControlsVisibility : MonoBehaviour
    {
        [SerializeField] private bool showInEditor = true;

        private void Awake()
        {
            bool show = Application.isMobilePlatform || Touchscreen.current != null || (Application.isEditor && showInEditor);
            gameObject.SetActive(show);
        }
    }
}
