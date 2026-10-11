using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Add this to each title-screen Button (Start/Continue/Exit) alongside the existing
// TitleMenu.onClick listeners set up by TitleSceneBuilder. It only adds sound; it never
// replaces or re-wires TitleMenu's own StartNewGame/ContinueGame/QuitGame listeners.
//
// Hover and UI focus have separate sounds; TitleMenu plays the title animation on click.
[RequireComponent(typeof(Button))]
public sealed class TitleButtonSfx : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public void OnPointerEnter(PointerEventData eventData) => SoundManager.Instance?.PlayTitleChoose();
    public void OnSelect(BaseEventData eventData) => SoundManager.Instance?.PlayTitleClick();
}
