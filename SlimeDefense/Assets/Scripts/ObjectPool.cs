using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hands out reused copies of a prefab instead of creating a fresh one every
/// time, and parks them again instead of destroying them.
///
/// This is Phase 8 Part D, and it exists for one reason: Instantiate and Destroy
/// are the two most expensive things this game does per frame, and it does them
/// constantly. A wave is dozens of slimes; a dozen towers firing once a second
/// is a projectile created and destroyed every few frames for the length of a
/// run. Each Destroy also leaves managed memory for the garbage collector, and a
/// collection that runs mid-wave is exactly the hitch that reads as the game
/// stuttering — on mobile and WebGL especially, which is where Phase 10 is
/// going.
///
/// The pool is keyed by prefab, so one pool serves every prefab in the game
/// rather than each spawner owning a pool of its own. Slimes and projectiles are
/// what go through it. Towers deliberately do not: a handful exist, they are
/// built and sold by hand, and pooling them would buy nothing while adding a
/// reset path to get wrong.
///
/// **The contract for a pooled object**, and the whole reason this is worth
/// reading before using it: a pooled object is *deactivated, not destroyed*, and
/// reactivated rather than recreated. So `Awake` and `Start` run once in the
/// object's entire lifetime, however many lives it lives, while `OnEnable` and
/// `OnDisable` run once per life. Anything that has to be true at the start of a
/// life — full health, cleared timers, an unsubscribed event — belongs in
/// `OnEnable`. Anything left in `Awake` is set once and then inherited by every
/// reuse, which is how a pooled enemy comes back from the dead already dying.
/// <see cref="Slime"/> and <see cref="HealthBar"/> are both written to that rule.
/// </summary>
public class ObjectPool : MonoBehaviour
{
    // Idle copies, keyed by the prefab they came from. A Stack rather than a
    // Queue on purpose: the most recently parked object is the one most likely
    // still warm in cache, and nothing here cares about fairness between copies.
    readonly Dictionary<GameObject, Stack<GameObject>> idle = new Dictionary<GameObject, Stack<GameObject>>();

    static ObjectPool instance;

    // Set when the application is shutting down. Spawning during teardown would
    // create objects Unity is in the middle of destroying, and lazily creating
    // the pool itself at that moment logs an error about creating a GameObject
    // during OnDestroy. Everything simply falls back to plain Destroy instead.
    static bool quitting;

    /// <summary>
    /// The pool, created on first use.
    ///
    /// Self-creating rather than dragged into the scene, because a pool that has
    /// to be placed by hand is a pool that is missing in one scene and silently
    /// turns every spawn into an Instantiate. It is a plain scene object and
    /// deliberately *not* DontDestroyOnLoad, for the same reason
    /// <see cref="GameManager"/> is not: a pool that survived a restart would
    /// hand out slimes still holding references to the previous run's route.
    /// </summary>
    public static ObjectPool Instance
    {
        get
        {
            // The == on a UnityEngine.Object reports null for a destroyed one,
            // so this also covers the pool that went away with the last scene.
            if (instance != null)
            {
                return instance;
            }

            if (quitting)
            {
                return null;
            }

            // Any, not First: there is only ever one, and asking for "any"
            // skips the instance-ID ordering pass that First pays for.
            instance = FindAnyObjectByType<ObjectPool>();

            if (instance == null)
            {
                instance = new GameObject("Object Pool").AddComponent<ObjectPool>();
            }

            return instance;
        }
    }

    // Statics survive Play mode when Enter Play Mode Options are set to skip the
    // domain reload, which would leave the second run holding the first run's
    // destroyed pool and a quitting flag stuck on. SubsystemRegistration runs
    // before any scene loads, on every entry into Play mode, which is the one
    // place to undo that.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        quitting = false;

        // Removed before it is added, and a named method rather than a lambda so
        // that the removal can find it. Application.quitting is a static of
        // Unity's own, so it is not reset between runs either — without this,
        // skipping the domain reload leaves one more subscriber on it every time
        // Play is pressed.
        Application.quitting -= OnApplicationQuitting;
        Application.quitting += OnApplicationQuitting;
    }

    static void OnApplicationQuitting()
    {
        quitting = true;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Returns a copy of <paramref name="prefab"/> at the given place, reusing a
    /// parked one if there is one. The component overload so callers keep the
    /// type they asked for: <c>Spawn(projectilePrefab, ...)</c> hands back a
    /// <see cref="Projectile"/>, not a GameObject to go looking on.
    /// </summary>
    public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        where T : Component
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject spawned = Spawn(prefab.gameObject, position, rotation, parent);
        return spawned != null ? spawned.GetComponent<T>() : null;
    }

    /// <summary>
    /// Returns a copy of <paramref name="prefab"/> at the given place, reusing a
    /// parked one if there is one.
    /// </summary>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null)
        {
            return null;
        }

        ObjectPool pool = Instance;

        // Only during teardown, when there is no pool to ask. A plain copy still
        // behaves correctly; it simply never comes back.
        if (pool == null)
        {
            return Instantiate(prefab, position, rotation, parent);
        }

        return pool.Take(prefab, position, rotation, parent);
    }

    /// <summary>
    /// Parks <paramref name="pooled"/> for reuse, or destroys it if it did not
    /// come from the pool. Safe to call twice on the same object, and safe to
    /// call on something that was never pooled — so a caller never has to know
    /// which of the two it is holding.
    /// </summary>
    public static void Despawn(GameObject pooled)
    {
        if (pooled == null)
        {
            return;
        }

        ObjectPool pool = Instance;

        if (pool == null)
        {
            Destroy(pooled);
            return;
        }

        pool.Return(pooled);
    }

    /// <summary>
    /// Creates <paramref name="count"/> copies up front and parks them, so the
    /// cost of building them lands before the run starts rather than on the
    /// frame a wave arrives.
    ///
    /// This is the half of pooling that is easy to leave out and then wonder why
    /// the first wave still hitches: an empty pool is exactly as expensive as no
    /// pool for the first slime of every type, and a wave's worth of first
    /// slimes all land inside a second or two of each other.
    /// </summary>
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
        {
            return;
        }

        ObjectPool pool = Instance;

        if (pool == null)
        {
            return;
        }

        Stack<GameObject> stack = pool.GetStack(prefab);

        for (int i = 0; i < count; i++)
        {
            GameObject copy = pool.Create(prefab, Vector3.zero, Quaternion.identity, pool.transform);

            copy.SetActive(false);
            copy.GetComponent<PooledInstance>().IsIdle = true;
            stack.Push(copy);
        }
    }

    GameObject Take(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        Stack<GameObject> stack = GetStack(prefab);
        GameObject pooled = null;

        // Popping in a loop rather than once, because a parked object can still
        // be destroyed out from under the pool — a scene teardown does exactly
        // that — and a destroyed one has to be skipped rather than handed out.
        while (pooled == null && stack.Count > 0)
        {
            pooled = stack.Pop();
        }

        // Placed by Instantiate itself rather than moved afterwards, so a brand
        // new copy runs its Awake and OnEnable already standing where it was
        // asked for — the same order a reused one gets below.
        if (pooled == null)
        {
            return Create(prefab, position, rotation, parent);
        }

        // Parented before it is placed, and with worldPositionStays false, which
        // matches what Instantiate(prefab, position, rotation, parent) does:
        // world position and rotation as asked, and the prefab's own local scale
        // kept rather than recomputed against the new parent.
        pooled.transform.SetParent(parent, false);
        pooled.transform.SetPositionAndRotation(position, rotation);

        // Cleared before the object wakes, so anything reading it inside its own
        // OnEnable sees an object in play rather than one still parked.
        pooled.GetComponent<PooledInstance>().IsIdle = false;
        pooled.SetActive(true);

        return pooled;
    }

    void Return(GameObject pooled)
    {
        PooledInstance receipt = pooled.GetComponent<PooledInstance>();

        // Something the pool never made. Destroying it is the honest answer: the
        // caller asked for it to go away, and the pool has nowhere to put it.
        if (receipt == null || receipt.Origin == null)
        {
            Destroy(pooled);
            return;
        }

        if (receipt.IsIdle)
        {
            return;
        }

        receipt.IsIdle = true;

        // Deactivated first, so the object's OnDisable runs while it still has
        // whatever parent and position it died at. Reparenting a live object
        // then switching it off would fire OnDisable from wherever the pool
        // happens to sit.
        pooled.SetActive(false);
        pooled.transform.SetParent(transform, false);

        GetStack(receipt.Origin).Push(pooled);
    }

    GameObject Create(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject copy = Instantiate(prefab, position, rotation, parent);

        // Dropping Unity's "(Clone)" suffix. A pool full of parked objects is
        // already a crowded Hierarchy; it reads better without it.
        copy.name = prefab.name;

        PooledInstance receipt = copy.AddComponent<PooledInstance>();
        receipt.Origin = prefab;

        return copy;
    }

    Stack<GameObject> GetStack(GameObject prefab)
    {
        if (!idle.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            idle.Add(prefab, stack);
        }

        return stack;
    }
}
