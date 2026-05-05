using UnityEngine;
using System.Collections.Generic;

// Manages the queue at the cash register
public class CashRegister : MonoBehaviour
{
    [Header("Queue Settings")]
    [SerializeField] private Transform queueStartPoint;
    [SerializeField] private float queueSpacing = 0.8f;

    private Queue<NPCBrain> _queue = new Queue<NPCBrain>();

    // Returns position for new NPC in queue
    public Vector3 GetQueuePosition()
    {
        int position = _queue.Count;
        return queueStartPoint.position - queueStartPoint.forward * (position * queueSpacing);
    }

    public void JoinQueue(NPCBrain npc)
    {
        _queue.Enqueue(npc);
        npc.OnNPCLeft += () => LeaveQueue(npc);
        Debug.Log($"[CashRegister] {npc.Data.npcName} joined queue. Queue size: {_queue.Count}");
    }

    private void LeaveQueue(NPCBrain npc)
    {
        // Rebuild queue without this NPC
        var temp = new List<NPCBrain>(_queue);
        temp.Remove(npc);
        _queue = new Queue<NPCBrain>(temp);
    }
}