using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections;
    private List<NetworkObjects.NetworkPlayer> m_Players;

    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        m_Players = new List<NetworkObjects.NetworkPlayer>(16);

        InvokeRepeating("serverUpdate", 0.1f, 0.1f);
    }

    void serverUpdate()
    {
        ServerUpdateMsg m = new ServerUpdateMsg();
        if(m_Connections.Length > 0 && m_Players.Count > 0)
        {
            for (int i = 0; i < m_Connections.Length; i++)
            {
                m.players.Add(new NetworkObjects.NetworkPlayer());
                m.players[i].id = m_Players[i].id;
                for (int k = 0; k < m_Players.Count; k++)
                {
                    if (m_Players[k].id == m.players[i].id)
                    {
                        m.players[i].cubePos = m_Players[k].cubePos;
                    }
                }
            }
            for (int i = 0; i < m_Connections.Length; i++)
            {
                SendToClient(JsonUtility.ToJson(m), m_Connections[i]);
            }
        }
    }

    void SendToClient(string message, NetworkConnection c){
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }
    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void OnConnect(NetworkConnection c){
        NetworkObjects.NetworkPlayer temp = new NetworkObjects.NetworkPlayer();
        temp.id = c.InternalId.ToString();
        temp.timeOfLastMsg = Time.time;

        if(m_Players.Count > 0)
        {
            for (int i = 0; i < m_Players.Count; i++)
            {
                ServerNewPlyrMsg m = new ServerNewPlyrMsg();
                m.player = temp;
                SendToClient(JsonUtility.ToJson(m), m_Connections[i]);
            }
        }

        m_Connections.Add(c);
        m_Players.Add(temp);
        Debug.Log("Accepted a connection");       
    }

    void movePlayer(PlayerUpdateMsg puMsg)
    {
        Vector3 temp = puMsg.player.cubePos;
        if(puMsg.player.movingLeft == 1 && puMsg.player.movingRight == 1)
        {
            //no movement
        }
        else if(puMsg.player.movingLeft == 0 && puMsg.player.movingRight == 0)
        {
            //no movement
        }
        else if(puMsg.player.movingLeft == 1 && puMsg.player.movingRight == 0)
        {
            temp.x -= 1;
        }
        else if(puMsg.player.movingLeft == 0 && puMsg.player.movingRight == 1)
        {
            temp.x += 1;
        }

        if(puMsg.player.movingForward == 1 && puMsg.player.movingBackward == 1)
        {
            //no movement
        }
        else if (puMsg.player.movingForward == 0 && puMsg.player.movingBackward == 0)
        {
            //no movement
        }
        else if (puMsg.player.movingForward == 1 && puMsg.player.movingBackward == 0)
        {
            temp.z += 1;
        }
        else if (puMsg.player.movingForward == 0 && puMsg.player.movingBackward == 1)
        {
            temp.z -= 1;
        }

        for(int i = 0; i < m_Players.Count; i++)
        {
            if (m_Players[i].id == puMsg.player.id)
            {
                m_Players[i].cubePos = temp;
                m_Players[i].timeOfLastMsg = Time.time;
            }
        }
    }

    void OnData(DataStreamReader stream, int i){
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
            movePlayer(puMsg);
            //Debug.Log("Player update message received!");
            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            Debug.Log("Server update message received!");
            break;
            default:
            Debug.Log("SERVER ERROR: Unrecognized message received!");
            break;
        }
    }

    void OnDisconnect(int i){
        if (m_Players.Count > 0)
        {
            for (int k = 0; k < m_Players.Count; k++)
            {
                ServerPlyrLeftMsg m = new ServerPlyrLeftMsg(); // may have to instantiate a new player for the message
                m.player = m_Players[i];
                SendToClient(JsonUtility.ToJson(m), m_Connections[k]);
            }
        }

        Debug.Log("Client disconnected from server");
        m_Connections[i] = default(NetworkConnection);
        m_Players.RemoveAt(i);
    }

    void Update ()
    {
        m_Driver.ScheduleUpdate().Complete();

        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {

                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }

        // AcceptNewConnections
        NetworkConnection c = m_Driver.Accept();
        while (c  != default(NetworkConnection))
        {            
            OnConnect(c);

            // Check if there is another new connection
            c = m_Driver.Accept();
        }

        //remove Inactive players
        for(int i = 0; i < m_Players.Count; i++)
        {
            if (Time.time - m_Players[i].timeOfLastMsg > 5.0f)
            {
                OnDisconnect(i);
            }
        }

        // Read Incoming Messages
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated);
            
            NetworkEvent.Type cmd;
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            while (cmd != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream, i);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
        }
    }
}