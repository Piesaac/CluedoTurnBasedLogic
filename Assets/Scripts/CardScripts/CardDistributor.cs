using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CardList;
using System.Linq;
using Unity.Netcode;

public class CardDistributor : NetworkBehaviour
{
    //creates list of list for all player hands 
    public List<List<Card>> playerHands = new List<List<Card>>();
    
    //creates list of all cards for clues
    private List<Card> allCards = new List<Card>();
    
    //creates list for evidence clues in envelope
    public List<Card> evidence = new List<Card>();

    private void Start()
    {
        //only host can start distribution
        if (!IsServer) return;

        //number of humans passed, the distrbute cards method handles the number of ais
        int numHumans = NetworkManager.Singleton.ConnectedClients.Count;
        distributeCards(numHumans);
    }

    public void distributeCards(int humanPlayerCount)
    {
        int totalPlayers = humanPlayerCount + MenuController.numBotsToSpawn;

        allCards.Clear();
        playerHands.Clear();
        evidence.Clear();

        //initlaise hands 
        for (int i = 0; i < totalPlayers; i++)
        {
            playerHands.Add(new List<Card>());
        }

        //seperates all evidence enums into separate lists for selection
        List<Card> suspects = System.Enum.GetValues(typeof(Who)).Cast<Who>()
            .Select(v => new Card { type = Card.CardType.Suspect, value = (int)v }).ToList();

        List<Card> weapons = System.Enum.GetValues(typeof(What)).Cast<What>()
            .Select(v => new Card { type = Card.CardType.Weapon, value = (int)v }).ToList();

        List<Card> rooms = System.Enum.GetValues(typeof(Where)).Cast<Where>()
            .Select(v => new Card { type = Card.CardType.Room, value = (int)v }).ToList();

        //chooses one card of each clue type for the evidence envelope
        evidence.Add(chooseEvidence(suspects));
        evidence.Add(chooseEvidence(weapons));
        evidence.Add(chooseEvidence(rooms));

        //adds all remaining cards into the master list for dealing
        allCards.AddRange(suspects);
        allCards.AddRange(weapons);
        allCards.AddRange(rooms);

        //shuffle the cards
        for (int i = 0; i < allCards.Count; i++)
        {
            Card temp = allCards[i];
            int randomIndex = Random.Range(i, allCards.Count);
            allCards[i] = allCards[randomIndex];
            allCards[randomIndex] = temp;
        }

        //distribute cards across all player hands 
        for (int i = 0; i < allCards.Count; i++)
        {
            int targetPlayerIndex = i % totalPlayers;
            playerHands[targetPlayerIndex].Add(allCards[i]);
        }

        // 6. Handle Networking and Logging
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        
        Debug.Log("CARD DISTRIBUTION SUMMARY");
        
        for (int i = 0; i < totalPlayers; i++)
        {
            string handContents = string.Join(", ", playerHands[i].Select(c => whatCard(c)));

            //if the index belongs to a human client
            if (i < clients.Count)
            {
                Card[] handToSend = playerHands[i].ToArray();

                //target only this specific client for the RPC
                ClientRpcParams rpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clients[i].ClientId } }
                };

                sendHandClientRPC(handToSend, rpcParams);
                Debug.Log($"Player {i} (Human ID: {clients[i].ClientId}) hand: {handContents}");
            }
            else
            {
                Debug.Log($"Player {i} (AI) hand: {handContents}");
            }
        }

        //debug log for the envelope contents
        string answerStr = string.Join(", ", evidence.Select(c => whatCard(c)));
        Debug.Log($"<color=red>THE ENVELOPE CONTAINS: {answerStr}</color>");
    }

    [ClientRpc]

    //method for showing the user what cards they have
    private void sendHandClientRPC(Card[] myCards, ClientRpcParams rpcParams = default)
    {
        if (UIController.Instance != null)
        {
            UIController.Instance.updateHand(myCards);
        }
    }

    //picks the cards for the envelope and removes them, so noone has them in their hand
    private Card chooseEvidence(List<Card> list)
    {
        int index = Random.Range(0, list.Count);
        Card picked = list[index];
        list.RemoveAt(index);
        return picked;
    }

    //method to convert card values into readable strings
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