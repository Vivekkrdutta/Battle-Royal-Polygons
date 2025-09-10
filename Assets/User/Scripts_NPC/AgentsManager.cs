using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


namespace AI
{
    /// <summary>
    /// Used for Handling the NPCs over the Network. For server only.
    /// </summary>
    public class AgentsManager : NetworkBehaviour
    {
        public static AgentsManager Instance { get; private set; }
        private readonly List<GameObject> agentsList = new();

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer) return;

            // only the server subscribes
            SmartAgentShootable.OnAnyAgentDead += SmartAgentShootable_OnAnyAgentDead;
        }

        private void SmartAgentShootable_OnAnyAgentDead(object sender, SmartAgentShootable.OnAnyAgentDeadEventArgs e)
        {
            if(agentsList.Contains(e.AgentShootable.gameObject)) agentsList.Remove(e.AgentShootable.gameObject);
        }

        public void RequestForDeploymentOfAgent(GameObject agent)
        {
            if (!IsServer || Player.PlayersList.Count == 0) return;

            if(agentsList.Contains(agent)) return;

            agentsList.Add(agent);

            switch (GameProperties.GameMode)
            {
                case GameProperties.Mode.OneVSOne:

                    StartCoroutine(SetClosestTarget(agent));
                    break;

                default:

                    break;
            }
        }

        private IEnumerator SetClosestTarget(GameObject agent)
        {
            yield return new WaitForSecondsRealtime(0.5f);

            var players = Player.PlayersList;
            float closestDistance = float.MaxValue;

            Player closestPLayer = null;

            foreach (var player in players)
            {
                if (player.GetComponent<ThirdPersonShooter>().GetAlive() == false) continue;
                if(closestPLayer == null || Vector3.Distance(transform.position, player.transform.position) < closestDistance)
                {
                    closestPLayer = player;
                    closestDistance = Vector3.Distance(transform.position, closestPLayer.transform.position);
                }
            }

            var selectedPlayer = closestPLayer;

            if(selectedPlayer == null)
            {
                StartCoroutine(RepeatedlyCheckForTargetForAgent(agent));
                yield break;
            }

            if(agent) agent.GetComponent<SmartAgentBehaviour>().SetTarget(selectedPlayer.transform);

            if(agentsList.Contains(agent)) agentsList.Remove(agent);
        }

        private IEnumerator RepeatedlyCheckForTargetForAgent(GameObject agent)
        {
            var targetset = false;
            while (!targetset)
            {
                Debug.Log("Repeatedly checking for agent");
                yield return new WaitForSecondsRealtime(3f);
                var players = Player.PlayersList;
                float closestDistance = float.MaxValue;

                Player closestPLayer = null;
                foreach (var player in players)
                {
                    if (player.GetComponent<ThirdPersonShooter>().GetAlive() == false) continue;

                    if (closestPLayer == null || Vector3.Distance(transform.position, player.transform.position) < closestDistance)
                    {
                        closestPLayer = player;
                        closestDistance = Vector3.Distance(transform.position, closestPLayer.transform.position);
                    }
                }

                var selectedPlayer = closestPLayer;

                if (selectedPlayer == null) continue;

                if (!agent) break;

                agent.GetComponent<SmartAgentBehaviour>().SetTarget(selectedPlayer.transform);

                break;
            }

            if (agentsList.Contains(agent)) agentsList.Remove(agent);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            SmartAgentShootable.OnAnyAgentDead -= SmartAgentShootable_OnAnyAgentDead;
        }
    }
}
