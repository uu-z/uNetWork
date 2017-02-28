using UnityEngine;
using UnityEngine.Networking;

public class LoginMsg : MessageBase {
    public static short MsgId = 10000;
    public string id;
}

public class ErrorMsg : MessageBase {
    public static short MsgId = 2000;
    public string text;
    public bool causesDisconnect;
}

public class LoginOkMsg : MessageBase {
    public static short MsgId = 2001;
}
