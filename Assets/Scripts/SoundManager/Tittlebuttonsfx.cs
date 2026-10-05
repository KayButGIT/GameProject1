using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Add this to each title-screen Button (Start/Continue/Exit) alongside the existing
// TitleMenu.onClick listeners set up by TitleSceneBuilder. It only adds sound; it never
// replaces or re-wires TitleMenu's own StartNewGame/ContinueGame/QuitGame listeners.
//
// OnPointerEnter covers mouse hover; OnSelect covers keyboard/gamepad navigation through the
// InputSystemUIInputModule already in the Title scene's Event System. Either path plays "choose".
[RequireComponent(typeof(Button))]
public sealed class TitleButtonSfx : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(PlayClick);
    }

    public void OnPointerEnter(PointerEventData eventData) => PlayChoose();
    public void OnSelect(BaseEventData eventData) => PlayChoose();

    private static void PlayChoose() => SoundManager.Instance?.PlayTitleChoose();
    private static void PlayClick() => SoundManager.Instance?.PlayTitleClick();
}