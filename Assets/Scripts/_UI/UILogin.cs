// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class UILogin : MonoBehaviour {
    [SerializeField] GameObject panel;
    [SerializeField] Text status;
    [SerializeField] InputField inputName;
    [SerializeField] InputField inputAddress;
    [SerializeField] Button btnLogin;
    [SerializeField] Button btnHost;
    [SerializeField] Button btnDedicated;
    [SerializeField] Button btnCancel;
    [SerializeField] Button btnQuit;

    // cache
    NewWorkController manager;

    void Awake() {
        // NetworkManager.singleton is null for some reason
        manager = FindObjectOfType<NewWorkController>();

        // button onclicks
        btnQuit.onClick.SetListener(() => { Application.Quit(); });
        btnLogin.onClick.SetListener(() => { manager.StartClient(); });
        btnHost.onClick.SetListener(() => { manager.StartHost(); });
        btnCancel.onClick.SetListener(() => { manager.StopClient(); });
        btnDedicated.onClick.SetListener(() => { manager.StartServer(); });
    }

    void Update() {
        // only update while visible
        if (!panel.activeSelf) return;

        // status
        status.text = manager.IsConnecting() ? "Connecting..." : "";

        // button states
        btnLogin.interactable = !manager.IsConnecting();
        btnHost.interactable = !manager.IsConnecting();
        btnCancel.gameObject.SetActive(manager.IsConnecting());
        btnDedicated.interactable = !manager.IsConnecting();

        // inputs
        manager.id = inputName.text;
        manager.networkAddress = inputAddress.text;
    }

    public void Show() { panel.SetActive(true); }
    public void Hide() { panel.SetActive(false); }
}
