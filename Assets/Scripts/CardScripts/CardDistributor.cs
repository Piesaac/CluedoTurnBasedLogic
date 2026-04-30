using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CardList;
using System.Linq;
using Unity.Netcode;

public class CardDistributor : NetworkBehaviour
{
    // Creates list of list for player hands (Includes Humans and AI)
    public List<List<Card>> playerHands = new List<List<Card>>();
    
    // Creates list of all cards for clues
    private List<Card> allCards = new List<Card>();
    
    // Creates list for evidence clues in envelope
    public List<Card> evidence = new List<Card>();

    private void Start()
    {
        // Ensures only the host can action distribution
        if (!IsServer) return;

        // Pass the number of connected humans; the method handles the AI addition
        int numHumans = NetworkManager.Singleton.ConnectedClients.Count;
        distributeCards(numHumans);
    }

    public void distributeCards(int humanPlayerCount)
    {
        int totalPlayers = humanPlayerCount + MenuController.numBotsToSpawn;

        allCards.Clear();
        playerHands.Clear();
        evidence.Clear();

        // 2. Initialize hands for EVERYONE (Humans + AI)
        for (int i = 0; i < totalPlayers; i++)
        {
            playerHands.Add(new List<Card>());
        }

        // Seperates all evidence enums into separate lists for selection.
        List<Card> suspects = System.Enum.GetValues(typeof(Who)).Cast<Who>()
            .Select(v => new Card { type = Card.CardType.Suspect, value = (int)v }).ToList();

        List<Card> weapons = System.Enum.GetValues(typeof(What)).Cast<What>()
            .Select(v => new Card { type = Card.CardType.Weapon, value = (int)v }).ToList();

        List<Card> rooms = System.Enum.GetValues(typeof(Where)).Cast<Where>()
            .Select(v => new Card { type = Card.CardType.Room, value = (int)v }).ToList();

        // 3. Chooses one card of each clue type for the evidence envelope
        evidence.Add(chooseEvidence(suspects));
        evidence.Add(chooseEvidence(weapons));
        evidence.Add(chooseEvidence(rooms));

        // Adds all remaining cards into the master list for dealing
        allCards.AddRange(suspects);
        allCards.AddRange(weapons);
        allCards.AddRange(rooms);

        // 4. Shuffle the cards
        for (int i = 0; i < allCards.Count; i++)
        {
            Card temp = allCards[i];
            int randomIndex = Random.Range(i, allCards.Count);
            allCards[i] = allCards[randomIndex];
            allCards[randomIndex] = temp;
        }

        // 5. Distribute cards across all player hands (modulo totalPlayers)
        for (int i = 0; i < allCards.Count; i++)
        {
            int targetPlayerIndex = i % totalPlayers;
            playerHands[targetPlayerIndex].Add(allCards[i]);
        }

        // 6. Handle Networking and Logging
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        
        Debug.Log("--- CARD DISTRIBUTION SUMMARY ---");
        
        for (int i = 0; i < totalPlayers; i++)
        {
            string handContents = string.Join(", ", playerHands[i].Select(c => whatCard(c)));

            // If the index belongs to a human client
            if (i < clients.Count)
            {
                Card[] handToSend = playerHands[i].ToArray();

                // Target only this specific client for the RPC
                ClientRpcParams rpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clients[i].ClientId } }
                };

                sendHandClientRPC(handToSend, rpcParams);
                Debug.Log($"Player {i} (Human ID: {clients[i].ClientId}) hand: {handContents}");
            }
            else
            {
                // This is an AI slot (Index >= Human Count)
                Debug.Log($"Player {i} (AI) hand: {handContents}");
            }
        }

        // Final Envelope Reveal (Server Only Log)
        string answerStr = string.Join(", ", evidence.Select(c => whatCard(c)));
        Debug.Log($"<color=red>THE ENVELOPE CONTAINS: {answerStr}</color>");
    }

    [ClientRpc]
    private void sendHandClientRPC(Card[] myCards, ClientRpcParams rpcParams = default)
    {
        if (UIController.Instance != null)
        {
            UIController.Instance.updateHand(myCards);
        }
    }

    private Card chooseEvidence(List<Card> list)
    {
        int index = Random.Range(0, list.Count);
        Card picked = list[index];
        list.RemoveAt(index);
        return picked;
    }

    public string whatCard(Card card)
    {
        return card.type switch
        {
            Card.CardType.Suspect => ((Who)card.value).ToString(),
            Card.CardType.Weapon  => ((What)card.value).ToString(),
            Card.CardType.Room    => ((Where)card.value).ToString(),
            _                     => "Unknown"
        };
    }
}