// This class adds functions to built-in types.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Xml;
using System.Text;
using System.Xml.Serialization;
using System.Linq;

public static class Extensions {

    // string to int (returns errVal if failed)
    public static int ToInt(this string value, int errVal=0) {
        Int32.TryParse(value, out errVal);
        return errVal;
    }

    // string to long (returns errVal if failed)
    public static long ToLong(this string value, long errVal=0) {
        Int64.TryParse(value, out errVal);
        return errVal;
    }

    // write xml node: <localname>value</localname>
    public static void WriteElementValue(this XmlWriter writer, string localName, object value) {
        writer.WriteStartElement(localName);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }

    // write xml node: <localname>Serialize(object)</localname>
    public static void WriteElementObject(this XmlWriter writer, string localName, object value) {
        writer.WriteStartElement(localName);
        var serializer = new XmlSerializer(value.GetType());
        serializer.Serialize(writer, value);
        writer.WriteEndElement();
    }

    // read xml node: <...>Unserialize(object)</...>
    public static T ReadElementObject<T>(this XmlReader reader) {
        var serializer = new XmlSerializer(typeof(T));
        reader.ReadStartElement();
        T value = (T)serializer.Deserialize(reader);
        reader.ReadEndElement();
        return value;
    }

    // transform.Find only finds direct children, no grandchildren etc.
    public static Transform FindRecursively(this Transform transform, string name) {
        return Array.Find(transform.GetComponentsInChildren<Transform>(true),
            t => t.name == name);

    }

    // FindIndex function for synclists
    public static int FindIndex<T>(this SyncList<T> list, Predicate<T> match) {
        for (int i = 0; i < list.Count; ++i)
            if (match(list[i]))
                return i;
        return -1;
    }

    // UI SetListener extension that removes previous and then adds new listener
    // (this version is for onClick etc.)
    public static void SetListener(this UnityEvent uEvent, UnityAction call) {
        uEvent.RemoveAllListeners();
        uEvent.AddListener(call);
    }

    // UI SetListener extension that removes previous and then adds new listener
    // (this version is for onEndEdit, onValueChanged etc.)
    public static void SetListener<T>(this UnityEvent<T> uEvent, UnityAction<T> call) {
        uEvent.RemoveAllListeners();
        uEvent.AddListener(call);
    }
}
