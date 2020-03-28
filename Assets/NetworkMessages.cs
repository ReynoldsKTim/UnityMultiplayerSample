using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkMessages
{
    public enum Commands{
        PLAYER_UPDATE,
        SERVER_UPDATE,
        HANDSHAKE,
        PLAYER_JOINED,
        PLAYER_LEFT
    }

    [System.Serializable]
    public class NetworkHeader{
        public Commands cmd;
    }

    [System.Serializable]
    public class HandshakeMsg:NetworkHeader{
        public NetworkObjects.NetworkPlayer player;
        public HandshakeMsg(){      // Constructor
            cmd = Commands.HANDSHAKE;
            player = new NetworkObjects.NetworkPlayer();
        }
    }
    
    [System.Serializable]
    public class PlayerUpdateMsg:NetworkHeader{
        public NetworkObjects.NetworkPlayer player;
        public PlayerUpdateMsg(){      // Constructor
            cmd = Commands.PLAYER_UPDATE;
            player = new NetworkObjects.NetworkPlayer();
        }
    };
    [System.Serializable]
    public class ServerNewPlyrMsg:NetworkHeader{
        public NetworkObjects.NetworkPlayer player;
        public ServerNewPlyrMsg(){
            cmd = Commands.PLAYER_JOINED;
            //player = new NetworkObjects.NetworkPlayer();
        }
    }
    [System.Serializable]
    public class ServerPlyrLeftMsg : NetworkHeader
    {
        public NetworkObjects.NetworkPlayer player;
        public ServerPlyrLeftMsg()
        {
            cmd = Commands.PLAYER_LEFT;
            //player = new NetworkObjects.NetworkPlayer();
        }
    }
    [System.Serializable]
    public class  ServerUpdateMsg:NetworkHeader{
        public List<NetworkObjects.NetworkPlayer> players;
        public ServerUpdateMsg(){      // Constructor
            cmd = Commands.SERVER_UPDATE;
            players = new List<NetworkObjects.NetworkPlayer>();
        }
    }
} 

namespace NetworkObjects
{
    [System.Serializable]
    public class NetworkObject{
        public string id;
    }
    [System.Serializable]
    public class NetworkPlayer : NetworkObject{
        public Color cubeColor;
        public Vector3 cubePos;
        public int movingRight;
        public int movingLeft;
        public int movingForward;
        public int movingBackward;
        public float timeOfLastMsg;

        public NetworkPlayer(){
            cubeColor = new Color();
            movingRight = 0;
            movingLeft = 0;
            movingForward = 0;
            movingBackward = 0;
        }
    }
}
