﻿// Copyright (c) 2012-2023 Wojciech Figat. All rights reserved.

#pragma once

#include "Types.h"
#include "Engine/Scripting/ScriptingObject.h"
#include "Engine/Scripting/ScriptingType.h"

template<typename T>
class DataContainer;

/// <summary>
/// The high-level network object role and authority. Used to define who owns the object and when it can be simulated or just replicated.
/// </summary>
API_ENUM(Namespace="FlaxEngine.Networking") enum class NetworkObjectRole : byte
{
    // Not replicated object.
    None = 0,
    // Server/client owns the object and replicates it to others. Only owning client can simulate object and provides current state.
    OwnedAuthoritative,
    // Server/client gets replicated object from other server/client who owns it. Object cannot be simulated locally (any changes will be overriden by replication).
    Replicated,
    // Client gets replicated object from server but still can locally autonomously simulate it too. For example, client can control local pawn with real human input but will validate with server proper state (eg. to prevent cheats).
    ReplicatedSimulated,
};

/// <summary>
/// High-level networking replication system for game objects.
/// </summary>
API_CLASS(static, Namespace = "FlaxEngine.Networking") class FLAXENGINE_API NetworkReplicator
{
    DECLARE_SCRIPTING_TYPE_MINIMAL(NetworkReplicator);
    friend class NetworkReplicatorInternal;
    typedef void (*SerializeFunc)(void* instance, NetworkStream* stream, void* tag);

public:
    /// <summary>
    /// Adds the network replication serializer for a given type.
    /// </summary>
    /// <param name="typeHandle">The scripting type to serialize.</param>
    /// <param name="serialize">Serialization callback method.</param>
    /// <param name="deserialize">Deserialization callback method.</param>
    /// <param name="serializeTag">Serialization callback method tag value.</param>
    /// <param name="deserializeTag">Deserialization callback method tag value.</param>
    static void AddSerializer(const ScriptingTypeHandle& typeHandle, SerializeFunc serialize, SerializeFunc deserialize, void* serializeTag = nullptr, void* deserializeTag = nullptr);

    /// <summary>
    /// Invokes the network replication serializer for a given type.
    /// </summary>
    /// <param name="typeHandle">The scripting type to serialize.</param>
    /// <param name="instance">The value instance to serialize.</param>
    /// <param name="stream">The input/output stream to use for serialization.</param>
    /// <param name="serialize">True if serialize, otherwise deserialize mode.</param>
    /// <returns>True if failed, otherwise false.</returns>
    API_FUNCTION(NoProxy) static bool InvokeSerializer(const ScriptingTypeHandle& typeHandle, void* instance, NetworkStream* stream, bool serialize);

    /// <summary>
    /// Adds the object to the network replication system.
    /// </summary>
    /// <remarks>Does nothing if network is offline.</remarks>
    /// <param name="obj">The object to replicate.</param>
    /// <param name="parent">The parent of the object (eg. player that spawned it).</param>
    API_FUNCTION() static void AddObject(ScriptingObject* obj, ScriptingObject* parent = nullptr);

    /// <summary>
    /// Removes the object from the network replication system.
    /// </summary>
    /// <remarks>Does nothing if network is offline.</remarks>
    /// <param name="obj">The object to don't replicate.</param>
    API_FUNCTION() static void RemoveObject(ScriptingObject* obj);

    /// <summary>
    /// Spawns the object to the other clients. Can be spawned by the owner who locally created it (eg. from prefab).
    /// </summary>
    /// <remarks>Does nothing if network is offline.</remarks>
    /// <param name="obj">The object to spawn on other clients.</param>
    API_FUNCTION() static void SpawnObject(ScriptingObject* obj);

    /// <summary>
    /// Spawns the object to the other clients. Can be spawned by the owner who locally created it (eg. from prefab).
    /// </summary>
    /// <remarks>Does nothing if network is offline.</remarks>
    /// <param name="obj">The object to spawn on other clients.</param>
    /// <param name="clientIds">List with network client IDs that should receive network spawn event. Empty to spawn on all clients.</param>
    API_FUNCTION() static void SpawnObject(ScriptingObject* obj, const DataContainer<uint32>& clientIds);

    /// <summary>
    /// Despawns the object from the other clients. Deletes object from remove clients.
    /// </summary>
    /// <remarks>Does nothing if network is offline.</remarks>
    /// <param name="obj">The object to despawn on other clients.</param>
    API_FUNCTION() static void DespawnObject(ScriptingObject* obj);

    /// <summary>
    /// Gets the Client Id of the network object owner.
    /// </summary>
    /// <param name="obj">The network object.</param>
    /// <returns>The Client Id.</returns>
    API_FUNCTION() static uint32 GetObjectOwnerClientId(ScriptingObject* obj);

    /// <summary>
    /// Gets the role of the network object used locally (eg. to check if can simulate object).
    /// </summary>
    /// <param name="obj">The network object.</param>
    /// <returns>The object role.</returns>
    API_FUNCTION() static NetworkObjectRole GetObjectRole(ScriptingObject* obj);

    /// <summary>
    /// Checks if the network object is owned locally (thus current client has authority to manage it).
    /// </summary>
    /// <remarks>Equivalent to GetObjectRole == OwnedAuthoritative.</remarks>
    /// <param name="obj">The network object.</param>
    /// <returns>True if object is owned by this client, otherwise false.</returns>
    API_FUNCTION() FORCE_INLINE static bool IsObjectOwned(ScriptingObject* obj)
    {
        return GetObjectRole(obj) == NetworkObjectRole::OwnedAuthoritative;
    }

    /// <summary>
    /// Checks if the network object is simulated locally (thus current client has can modify it - changed might be overriden by other client who owns this object).
    /// </summary>
    /// <remarks>Equivalent to GetObjectRole != Replicated.</remarks>
    /// <param name="obj">The network object.</param>
    /// <returns>True if object is simulated on this client, otherwise false.</returns>
    API_FUNCTION() FORCE_INLINE static bool IsObjectSimulated(ScriptingObject* obj)
    {
        return GetObjectRole(obj) != NetworkObjectRole::Replicated;
    }

    /// <summary>
    /// Checks if the network object is replicated locally (any local changes might be overriden by other client who owns this object).
    /// </summary>
    /// <remarks>Equivalent to (GetObjectRole == Replicated or GetObjectRole == ReplicatedAutonomous).</remarks>
    /// <param name="obj">The network object.</param>
    /// <returns>True if object is simulated on this client, otherwise false.</returns>
    API_FUNCTION() FORCE_INLINE static bool IsObjectReplicated(ScriptingObject* obj)
    {
        const NetworkObjectRole role = GetObjectRole(obj);
        return role == NetworkObjectRole::Replicated || role == NetworkObjectRole::ReplicatedSimulated;
    }

    /// <summary>
    /// Sets the network object ownership - owning client identifier and local role to use.
    /// </summary>
    /// <param name="obj">The network object.</param>
    /// <param name="ownerClientId">The new owner. Set to NetworkManager::LocalClientId for local client to be owner (server might reject it).</param>
    /// <param name="localRole">The local role to assign for the object.</param>
    /// <param name="hierarchical">True if apply the ownership to all child objects of this object (eg. all child actors and scripts attached to the networked actor).</param>
    API_FUNCTION() static void SetObjectOwnership(ScriptingObject* obj, uint32 ownerClientId, NetworkObjectRole localRole = NetworkObjectRole::Replicated, bool hierarchical = true);

    /// <summary>
    /// Marks the object dirty to perform immediate replication to the other clients.
    /// </summary>
    /// <param name="obj">The network object.</param>
    API_FUNCTION() static void DirtyObject(ScriptingObject* obj);

public:
    /// <summary>
    /// Begins invoking the RPC and returns the Network Stream to serialize parameters to.
    /// </summary>
    /// <returns>Network Stream to write RPC parameters to.</returns>
    API_FUNCTION() static NetworkStream* BeginInvokeRPC();

    /// <summary>
    /// Ends invoking the RPC.
    /// </summary>
    /// <param name="obj">The target object to invoke RPC.</param>
    /// <param name="type">The RPC type.</param>
    /// <param name="name">The RPC name.</param>
    /// <param name="argsStream">The RPC serialized arguments stream returned from BeginInvokeRPC.</param>
    static void EndInvokeRPC(ScriptingObject* obj, const ScriptingTypeHandle& type, const StringAnsiView& name, NetworkStream* argsStream);

private:
#if !COMPILE_WITHOUT_CSHARP
    API_FUNCTION(NoProxy) static void AddSerializer(const ScriptingTypeHandle& typeHandle, const Function<void(void*, void*)>& serialize, const Function<void(void*, void*)>& deserialize);
    API_FUNCTION(NoProxy) static void AddRPC(const ScriptingTypeHandle& typeHandle, const StringAnsiView& name, const Function<void(void*, void*)>& execute, bool isServer, bool isClient, NetworkChannelType channel);
    API_FUNCTION(NoProxy) static void CSharpEndInvokeRPC(ScriptingObject* obj, const ScriptingTypeHandle& type, const StringAnsiView& name, NetworkStream* argsStream);
    static StringAnsiView GetCSharpCachedName(const StringAnsiView& name);
#endif
};
