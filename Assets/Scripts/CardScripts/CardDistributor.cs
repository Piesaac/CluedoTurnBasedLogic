using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CardList;
using System.Linq;
using Unity.Netcode;

public class CardDistributor : NetworkBehaviour
{
    // Creates list of list for player hands
    private List<List<Card>> playerHands = new List<List<Card>>();
    // Creates list of all cards for clues
    private List<Card> allCards = new List<Card>();
    //Creates list for evidence clues in envelope
    public List<Card> evidence = new List<Card>();

    private void Start()
    {
        // Ensures only the host can action distribution
        if (!IsServer) return;
        // Finds number of players from connected clients and distributes cards accordingly
        int numPlayers = NetworkManager.Singleton.ConnectedClients.Count;
        distributeCards(numPlayers);
    }

    public void distributeCards(int playerCount)
    {
        // Clears any previous data to initialise a new game
        allCards.Clear();
        playerHands.Clear();
        evidence.Clear();

        // For each connected player, adds a new hand list into the list of all player hands
        for (int i = 0; i < playerCount; i++)
            playerHands.Add(new List<Card>());

        // Seperates all evidence enums into seperate lists for selection.
        List<Card> suspects = System.Enum.GetValues(typeof(Who)).Cast<Who>()
            .Select(v => new Card { type = Card.CardType.Suspect, value = (int)v }).ToList();

        List<Card> weapons = System.Enum.GetValues(typeof(What)).Cast<What>()
            .Select(v => new Card { type = Card.CardType.Weapon, value = (int)v }).ToList();

        List<Card> rooms = System.Enum.GetValues(typeof(Where)).Cast<Where>()
            .Select(v => new Card { type = Card.CardType.Room, value = (int)v }).ToList();

        // Chooses one card of each clue type for the evidence
        evidence.Add(chooseEvidence(suspects));
        evidence.Add(chooseEvidence(weapons));
        evidence.Add(chooseEvidence(rooms));

        // Adds all remaining cards into the list of all cards remaining
        allCards.AddRange(suspects);
        allCards.AddRange(weapons);
        allCards.AddRange(rooms);

        // Shuffles the cards using random indexes
        for (int i = 0; i < allCards.Count; i++)
        {
            Card temp = allCards[i];
            int randomIndex = Random.Range(i, allCards.Count);
            allCards[i] = allCards[randomIndex];
            allCards[randomIndex] = temp;
        }

        // Distributes cards across player hands depending on the player count
        for (int i = 0; i < allCards.Count; i++)
        {
            int targetPlayerIndex = i % playerCount;
            playerHands[targetPlayerIndex].Add(allCards[i]);
        }

        // Creates a list of all players connected
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        for (int i = 0; i < clients.Count; i++)
        {
            // For each player connected, creates an array of their cards in hand
            Card[] handToSend = playerHands[i].ToArray();

            // Ensure that each player is only sent their hand
            ClientRpcParams rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clients[i].ClientId } }
            };

            sendHandClientRPC(handToSend, rpcParams);
        }
        string answerStr = string.Join(", ", evidence.Select(c => whatCard(c)));
        Debug.Log($"<color=red>THE ENVELOPE CONTAINS: {answerStr}</color>");
        Debug.Log("--- CARD DISTRIBUTION SUMMARY ---");
        for (int i = 0; i < playerHands.Count; i++)
        {
            string handContents = string.Join(", ", playerHands[i].Select(c => whatCard(c)));
            Debug.Log($"Player {i} (ClientID: {clients[i].ClientId}) hand: {handContents}");
        }
    }

    // For each client connected, updates the hand UI to display their card list
    [ClientRpc]
    private void sendHandClientRPC(Card[] myCards, ClientRpcParams rpcParams = default)
    {
        if (UIController.Instance != null)
        {
            UIController.Instance.updateHand(myCards);
        }
    }

    // Mtheod to pick random card
    private Card chooseEvidence(List<Card> list)
    {
        int index = Random.Range(0, list.Count);
        Card picked = list[index];
        list.RemoveAt(index);
        return picked;
    }

    // Returns the name of the card selected
    private string whatCard(Card card)
    {
        return card.type switch
        {
            Card.CardType.Suspect => ((Who)card.value).ToString(),
            Card.CardType.Weapon => ((What)card.value).ToString(),
            Card.CardType.Room   => ((Where)card.value).ToString(),
            _                    => "Unknown"
        };
    }
}