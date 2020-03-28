using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Collections.Generic;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;
    public GameObject playerCube;
    Dictionary<string,GameObject> players;

    int movingRight = 0;
    int movingLeft = 0;
    int movingForward = 0;
    int movingBackward = 0;


    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);
        players  = new Dictionary<string, GameObject>();

        InvokeRepeating("playerUpdate", 0.1f, 0.1f);
    }

    void playerUpdate()
    {
        Debug.Log("PlayerUpdates");
        if(players.Count > 0)
        {
            PlayerUpdateMsg m = new PlayerUpdateMsg();
            m.player.id = m_Connection.InternalId.ToString();
            m.player.movingLeft = movingLeft;
            m.player.movingRight = movingRight;
            m.player.movingForward = movingForward;
            m.player.movingBackward = movingBackward;
            m.player.cubePos = players[m_Connection.InternalId.ToString()].transform.position;

            //Debug.Log(m.player.movingLeft);

            SendToServer(JsonUtility.ToJson(m));
        }
    }

    void SendToServer(string message){
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){
        Debug.Log("We are now connected to the server");
        //instantiate player
        players.Add(m_Connection.InternalId.ToString(), (GameObject)Instantiate(playerCube));
    }

    void updatePlayerPos(ServerUpdateMsg suMsg)
    {
        //update player positions from player list
        for(int i = 0; i < suMsg.players.Count; i++)
        {
            foreach(KeyValuePair<string, GameObject> entry in players)
            {
                if(suMsg.players[i].id == entry.Key)
                {
                    entry.Value.transform.position = suMsg.players[i].cubePos;
                }
            }
        }
    }

    void newPlayerJoined(ServerNewPlyrMsg newPlyrMsg)
    {
        players.Add(newPlyrMsg.player.id, (GameObject)Instantiate(playerCube));
        Debug.Log("New player added!");
    }

    void playerDisconnect(ServerPlyrLeftMsg plyrLeftMsg)
    {
        players.Remove(plyrLeftMsg.player.id);
        Debug.Log("Player Disconnected!");
    }

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received!");
            break;
            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Player update message received!");
            break;
            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                updatePlayerPos(suMsg);
                //Debug.Log("Server update message received!");
            break;
            case Commands.PLAYER_JOINED:
                ServerNewPlyrMsg newPlyrMsg = JsonUtility.FromJson<ServerNewPlyrMsg>(recMsg);
                newPlayerJoined(newPlyrMsg);
                Debug.Log("Player joined message received!");
            break;
            case Commands.PLAYER_LEFT:
                ServerPlyrLeftMsg plyrLeftMsg = JsonUtility.FromJson<ServerPlyrLeftMsg>(recMsg);
                playerDisconnect(plyrLeftMsg);
                Debug.Log("Player Left message received!");
            break;
            default:
                Debug.Log("Unrecognized message received!");
            break;
        }
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }
    NetworkObjects.NetworkPlayer myCube;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            movingForward = 1;
        }
        if (Input.GetKeyUp(KeyCode.UpArrow) || Input.GetKeyUp(KeyCode.W))
        {
            movingForward = 0;
        }
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            movingBackward = 1;
        }
        if (Input.GetKeyUp(KeyCode.DownArrow) || Input.GetKeyUp(KeyCode.S))
        {
            movingBackward = 0;
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            movingLeft = 1;
        }
        if (Input.GetKeyUp(KeyCode.LeftArrow) || Input.GetKeyUp(KeyCode.A))
        {
            movingLeft = 0;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            movingRight = 1;
        }
        if (Input.GetKeyUp(KeyCode.RightArrow) || Input.GetKeyUp(KeyCode.D))
        {
            movingRight = 0;
        }

        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
    }
}