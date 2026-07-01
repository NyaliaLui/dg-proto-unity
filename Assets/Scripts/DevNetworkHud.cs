using Unity.Netcode;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// TEMPORARY developer-only connect panel for testing networking before the
    /// real matchmaking menu exists. Shows Host / Client / Server buttons in the
    /// top-left while not connected, and a status line once connected.
    ///
    /// This is a Milestone-1 testing aid — Milestone 2 replaces it with the
    /// proper "Find Match" menu + Relay matchmaking. Safe to delete then.
    /// Uses IMGUI (OnGUI) so it needs no canvas/prefab wiring.
    /// </summary>
    public class DevNetworkHud : MonoBehaviour
    {
        [SerializeField] private bool showInBuilds = true;

        private void OnGUI()
        {
            if (!Application.isEditor && !showInBuilds) return;
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 220, 140), GUI.skin.box);
            GUILayout.Label("DEV NET (temporary)");

            if (!nm.IsClient && !nm.IsServer)
            {
                if (GUILayout.Button("Start Host")) nm.StartHost();
                if (GUILayout.Button("Start Client")) nm.StartClient();
                if (GUILayout.Button("Start Server")) nm.StartServer();
            }
            else
            {
                string role = nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client";
                GUILayout.Label($"Running as: {role}");
                GUILayout.Label($"Clients connected: {nm.ConnectedClientsList.Count}");
                if (GUILayout.Button("Shutdown")) nm.Shutdown();
            }

            GUILayout.EndArea();
        }
    }
}
