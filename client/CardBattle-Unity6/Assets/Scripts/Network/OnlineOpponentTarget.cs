using MyProjectF.Assets.Scripts.Player;
using UnityEngine;

/// <summary>
/// Marker on online opponent visual so card targeting can raycast it
/// (PvE used Enemy component; online opponent is a PlayerStats clone).
/// </summary>
public class OnlineOpponentTarget : MonoBehaviour
{
    public PlayerStats Stats;
}
