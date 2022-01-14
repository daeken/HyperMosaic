using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using NetLib;
using NetLib.Generated;
using Siccity.GLTFUtility;
using Unity.VisualScripting;
using UnityEngine.Playables;
using Object = NetLib.Generated.Object;
#pragma warning disable CS1998


public static class Extensions {
    public static Matrix4x4 ToUnity(this System.Numerics.Matrix4x4 mat) =>
        new(
            new Vector4(mat.M11, mat.M12, mat.M13, mat.M14),
            new Vector4(mat.M21, mat.M22, mat.M23, mat.M24), 
            new Vector4(mat.M31, mat.M32, mat.M33, mat.M34), 
            new Vector4(mat.M41, mat.M42, mat.M43, mat.M44)
        );
}
public class WorldBehaviour : MonoBehaviour
{
    class ClientRoot : BaseRoot {
        public ClientRoot(IConnection connection) : base(connection) {}
        public override async Task<string[]> ListInterfaces() => new[] { "hypercosm.object.v1.0.0", "hypercosm.root.v0.1.0" };
        public override async Task Release() {}
        public override async Task<string[]> ListExtensions() => new[] { "hypercosm.assetdelivery.v0.1.0", "hypercosm.world.v0.1.0" };
        public override async Task Ping() {
        }
        public override Task<Object> GetObjectById(Uuid id) {
            throw new NotImplementedException();
        }
        public override Task<Object> GetObjectByName(string name) {
            throw new NotImplementedException();
        }
    }

    readonly ConcurrentQueue<Action> MainThreadTasks = new();
    void OnMain(Action func) => MainThreadTasks.Enqueue(func);

    async void Start() {
        var tclient = new TcpClient("localhost", 12345);
        var stream = new SslStream(tclient.GetStream(), false, (_, _, _, _) => true);
        await stream.AuthenticateAsClientAsync("");

        Memory<byte> skb = new byte[16];
        Uuid.Generate().GetBytes(skb.Span);
        await stream.WriteAsync(skb);
        await stream.ReadAllAsync(skb);
        var nuid = new Uuid(skb.Span);

        var conn = new Connection(stream, conn => new ClientRoot(conn), Debug.Log);
        await conn.Handshake();

        var adObj = await conn.RemoteRoot.GetObjectByName("hypercosm.assetdelivery.v0.1.0");
        var assetDelivery = conn.GetObject(adObj.ObjectId, id => new RemoteAssetdelivery(conn, id));
        var wObj = await conn.RemoteRoot.GetObjectByName("hypercosm.world.v0.1.0");
        var world = conn.GetObject(wObj.ObjectId, id => new RemoteWorld(conn, id));

        await world.SubscribeAddEntities(async entities => {
            Debug.Log($"Got info about {entities.Length} entities!");

            foreach(var entity in entities) {
                Debug.Log("Getting asset for entity");
                var asset = await assetDelivery.FetchAssetById(entity.AssetId);
                Debug.Log("Got asset! Starting async load of GLB");
                OnMain(() =>
                    Importer.ImportGLBAsync(asset.Data, new() { animationSettings = { useLegacyClips = true } }, (obj, animations) => {
                        Debug.Log("Finished loading GLTF; adding.");
                        var holder = new GameObject("Transformer");
                        holder.transform.SetParent(gameObject.transform);
                        obj.transform.SetParent(holder.transform);
                        var um = entity.Transformation.ToUnity();
                        if(um.ValidTRS()) {
                            var ht = holder.transform;
                            ht.localPosition = new Vector3(um.m03, um.m13, um.m23);
                            ht.localRotation = um.rotation;
                            ht.localScale = um.lossyScale;
                        }
                        Debug.Log("Adding mesh colliders...");
                        void AddMeshColliders(Transform tf) {
                            foreach(var mr in tf.GetComponents<MeshRenderer>())
                                mr.AddComponent<MeshCollider>();
                            foreach(Transform child in tf)
                                AddMeshColliders(child);
                        }
                        AddMeshColliders(obj.transform);
                        if(animations != null && animations.Length != 0) {
                            var anim = animations[0];
                            anim.wrapMode = WrapMode.Loop;
                            var animator = obj.AddComponent<Animation>();
                            animator.AddClip(anim, anim.name);
                            animator.clip = animator.GetClip(anim.name);
                            animator.Play();
                            animator.wrapMode = WrapMode.Loop;
                        }
                        Debug.Log("Done adding asset");
                    })
                );
            }
            Debug.Log("Done with entity batch");
        });
    }

    // Update is called once per frame
    void Update() {
        while(MainThreadTasks.TryDequeue(out var func))
            func();
    }
}
