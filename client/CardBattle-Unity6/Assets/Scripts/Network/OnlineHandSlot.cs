using UnityEngine;
using UnityEngine.EventSystems;
using CardBattle.Network;
using MyProjectF.Assets.Scripts.Cards;
using MyProjectF.Assets.Scripts.Player;

/// <summary>
/// Marks a hand card with server hand index; supports click-to-play for online.
/// </summary>
public class OnlineHandSlot : MonoBehaviour, IPointerClickHandler
{
    public byte HandIndex;
    public bool isPlayed;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!OnlineSession.Active || !OnlineBattleController.AllowPlayerInput || isPlayed)
        {
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        // Ignore click if this was part of a drag.
        if (eventData.dragging)
        {
            return;
        }

        var card = GetComponent<CardMovement>()?.cardData
            ?? GetComponent<CardDisplay>()?.cardData;
        if (card != null && (PlayerManager.Instance == null || !PlayerManager.Instance.CanPlayCard(card)))
        {
            PlayerStats.Instance?.playerDisplay?.ShowEnergyDeniedEffect();
            Logger.Log("OnlineHandSlot: click play blocked — not enough energy.", this);
            return;
        }

        isPlayed = true;
        GameNetwork.Instance?.SendPlayCard(HandIndex);
        OnlineBattleController.LockInputUntilState();
        HandManager.Instance?.RemoveCardFromHandVisualOnly(gameObject);
    }
}
