// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using UnityEngine;
using UnityEngine.UI;

public class UIPopup : MonoBehaviour {
    [SerializeField] GameObject panel;
    [SerializeField] Text message;

    public void Show(string msg) {
        // already shown? then simply add error to it (we only have 1 popup)
        if (panel.activeSelf) {
            message.text += ";\n" + msg;
            // otherwise show and set new text
        } else {
            panel.SetActive(true);
            message.text = msg;
        }
    }
}
