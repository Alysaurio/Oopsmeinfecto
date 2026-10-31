using UnityEngine;
using UnityEngine.EventSystems;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Displays or hides a target tooltip GameObject based on mouse pointer hover.
    /// </summary>
    public sealed class InfoTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject _tooltipPanel;

        public GameObject TooltipPanel
        {
            get => _tooltipPanel;
            set => _tooltipPanel = value;
        }

        public void Initialize(GameObject tooltipPanel)
        {
            _tooltipPanel = tooltipPanel;
            if (_tooltipPanel != null)
            {
                _tooltipPanel.SetActive(false);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_tooltipPanel != null)
            {
                _tooltipPanel.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_tooltipPanel != null)
            {
                _tooltipPanel.SetActive(false);
            }
        }
    }
}
