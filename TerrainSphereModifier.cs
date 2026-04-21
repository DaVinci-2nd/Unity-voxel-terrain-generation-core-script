using System.Collections.Generic;
using UnityEngine;

public class TerrainSphereModifier : MonoBehaviour
{
    public float radius = 3f;
    public float strength = 2f;
    public int maxHits = 64;

    Collider[] hits;
    readonly HashSet<TerrainChunkRelay> relays = new HashSet<TerrainChunkRelay>();

    void Awake()
    {
        if (maxHits < 1)
        {
            maxHits = 1;
        }

        hits = new Collider[maxHits];
    }

    public int ModifyNow()
    {
        if (hits == null || hits.Length != maxHits)
        {
            hits = new Collider[maxHits];
        }

        relays.Clear();

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            hits,
            1 << 3,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            TerrainChunkRelay relay = hits[i].GetComponent<TerrainChunkRelay>();
            if (relay == null)
            {
                relay = hits[i].GetComponentInParent<TerrainChunkRelay>();
            }

            if (relay != null)
            {
                relays.Add(relay);
            }
        }

        int changedCount = 0;

        foreach (TerrainChunkRelay relay in relays)
        {
            if (relay != null && relay.ModifySphere(transform.position, radius, strength))
            {
                changedCount++;
            }
        }

        return changedCount;
    }

    public int AddNow()
    {
        if (hits == null || hits.Length != maxHits)
        {
            hits = new Collider[maxHits];
        }

        relays.Clear();

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            hits,
            1 << 3,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            TerrainChunkRelay relay = hits[i].GetComponent<TerrainChunkRelay>();
            if (relay == null)
            {
                relay = hits[i].GetComponentInParent<TerrainChunkRelay>();
            }

            if (relay != null)
            {
                relays.Add(relay);
            }
        }

        int changedCount = 0;

        foreach (TerrainChunkRelay relay in relays)
        {
            if (relay != null && relay.AddSphere(transform.position, radius, strength))
            {
                changedCount++;
            }
        }

        return changedCount;
    }
}