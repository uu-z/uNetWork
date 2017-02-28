using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Linq;

public class Database {
    // database path: Application.dataPath is always relative to the project,
    // but we don't want it inside the Assets folder in the Editor (git etc.),
    // instead we put it above that.
    // we also use Path.Combine for platform independent paths
    // and we need persistentDataPath on android

    // helper functions ////////////////////////////////////////////////////////
    #if UNITY_EDITOR
    static string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Database");
    #elif UNITY_ANDROID
    static string path = Path.Combine(Application.persistentDataPath, "Database");
    #elif UNITY_IOS
    static string path = Path.Combine(Application.persistentDataPath, "Database");
    #else
    static string path = Path.Combine(Application.dataPath, "Database");
    #endif

    static string CharactersPath() {
        return Path.Combine(path, "Characters");
    }

    public static string CharacterPath(string charName) {
        return Path.Combine(CharactersPath(), charName);
    }

    // character saving ////////////////////////////////////////////////////////
    public static bool CharacterExists(string charName) {
        return File.Exists(CharacterPath(charName));
    }

    public static void CharacterSave(Player player) {
        Directory.CreateDirectory(CharactersPath()); // force directory

        var settings = new XmlWriterSettings{ Encoding = Encoding.UTF8,Indent = true };

        using (var writer = XmlWriter.Create(CharacterPath(player.name), settings)) {
            writer.WriteStartDocument();
            writer.WriteStartElement("character");

            writer.WriteElementObject("position", player.transform.position);
            writer.WriteElementValue("hp", player.hp);
            writer.WriteElementValue("toolbarSelection", player.toolbarSelection);
            writer.WriteEndDocument();
        }
    }

    public static GameObject CharacterLoad(string charName, GameObject playerPrefab) {
        string fpath = CharacterPath(charName);
        if (File.Exists(fpath)) {
            var settings = new XmlReaderSettings{ IgnoreWhitespace = true };

            using (XmlReader reader = XmlReader.Create(fpath, settings)) {
                reader.ReadStartElement("character");

                // instantiate
                if (playerPrefab != null) {
                    var go = (GameObject)GameObject.Instantiate(playerPrefab);
                    var player = go.GetComponent<Player>();

                    player.name               = charName;
                    player.transform.position = reader.ReadElementObject<Vector3>();
                    Debug.LogWarning("TODO verify that position is not inside a block");
                    player.hp                 = reader.ReadElementContentAsInt();
                    player.toolbarSelection   = reader.ReadElementContentAsInt();

                    reader.ReadEndElement();

                    return go;
                }
            }
        }
        Debug.LogWarning("couldnt load character data:" + fpath);
        return null;
    }


    public static void CharacterCreate(string charName, GameObject playerPrefab, Transform startPos) {
        // we instantiate a temporary player, set the default values and then
        // save it
        if (playerPrefab != null) {
            var go = (GameObject)GameObject.Instantiate(playerPrefab);
            var player = go.GetComponent<Player>();

            player.name = charName;
            player.transform.position = startPos.position;
            Debug.LogWarning("TODO verify that startpos is not inside a block or prevent from building one there");
            player.hp = player.hpMax;

            // save it once to make sure that it does exist even if the server
            // crashes during the next few seconds. it's just better that way.
            CharacterSave(player);

            // destroy temporary player object again
            GameObject.Destroy(go);
        } else Debug.LogWarning("couldnt create character:" + charName);
    }
}