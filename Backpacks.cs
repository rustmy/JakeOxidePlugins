using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Plugins;

namespace Oxide.Plugins
{
    [Info("Backpacks", "Jake_Rich", 0.1)]
    [Description("Backpacks to carry extra items.")]

    public class Backpacks : RustPlugin
    {
        public static Backpacks thisPlugin;

        #region Settings
        public static int displayMode = 0; //0 = Only from player's belt
                                           //1 = From player's belt and inventory
        #endregion

        void Loaded()
        {
            thisPlugin = this;
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                player.SendConsoleCommand("bind tab inventory.toggle;backpackInvButton");
                Puts(player.inventory.containerWear.canAcceptItem.Method.Name.ToString());
            }
        }

        void CanAcceptItem()
        {
            Puts("Can accept item");
        }

        void Unload()
        {
            foreach (BaseEntity entity in fakeEntities)
            {
                if (!entity.isDestroyed)
                {
                    entity.Kill();
                }
            }

            foreach(BasePlayer player in fakePlayers)
            {
                if (!player.isDestroyed)
                {
                    player.Kill();
                }
            }
        }

        #region Writing
        public void Output(params object[] text)
        {
            string str = "";
            for (int i = 0; i < text.Length; i++)
            {
                str += text[i].ToString() + " ";
            }
            Puts(str);
        }

        public static void Write(params object[] text)
        {
            thisPlugin.Output(text);
        }

        public static void Write(object text)
        {
            thisPlugin.Output(text);
        }

        #endregion

        public static Dictionary<BasePlayer, MyBackpackComponent> backpackData = new Dictionary<BasePlayer, MyBackpackComponent>();
        public static List<BaseNetworkable> fakeEntities = new List<BaseNetworkable>();
        public static List<BasePlayer> fakePlayers = new List<BasePlayer>();
        public static Dictionary<BaseEntity, BasePlayer> backpackNetworking = new Dictionary<BaseEntity, BasePlayer>();

        void SpawnEntity(Vector3 pos, BasePlayer player)
        {
            string prefab = "assets/prefabs/ammo/arrow/arrow.prefab";
            BaseEntity entity = GameManager.server.CreateEntity(prefab);
            if (entity != null)
            {
                entity.Spawn();
                entity.SetParent(player, "RustPlayer");
                //entity.SetParent(bear, "RustPlayer");
                entity.transform.position = new Vector3(0, 0, 0);
                entity.transform.rotation = Quaternion.Euler(0, 0, 0);
                entity.SendNetworkUpdateImmediate();
                fakeEntities.Add(entity);
                Write("Object spawned");
            }
        }

        object CanNetworkTo(BaseNetworkable entity, BasePlayer player)
        {
            if (backpackNetworking.ContainsKey((BaseEntity)entity)) //If it crashes, change it to net.ID
            {
                if (backpackNetworking[(BaseEntity)entity] == player)
                {
                    Puts("dont network to player");
                    return true;
                }
            }
            return null;
        }

        //assets/prefabs/deployable/large wood storage/box.wooden.large.prefab
        //assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab
        //assets/prefabs/deployable/small stash/small_stash_deployed.prefab

        /*
        885 - assets/bundled/prefabs/ui/lootpanels/lootpanel.autoturret.prefab
        886 - assets/bundled/prefabs/ui/lootpanels/lootpanel.campfire.prefab
        887 - assets/bundled/prefabs/ui/lootpanels/lootpanel.crate.prefab
        888 - assets/bundled/prefabs/ui/lootpanels/lootpanel.fuelstorage.prefab
        889 - assets/bundled/prefabs/ui/lootpanels/lootpanel.furnace.prefab
        890 - assets/bundled/prefabs/ui/lootpanels/lootpanel.generic.prefab
        891 - assets/bundled/prefabs/ui/lootpanels/lootpanel.lantern.prefab
        892 - assets/bundled/prefabs/ui/lootpanels/lootpanel.largefurnace.prefab
        893 - assets/bundled/prefabs/ui/lootpanels/lootpanel.largewoodbox.prefab
        894 - assets/bundled/prefabs/ui/lootpanels/lootpanel.player_corpse.prefab
        895 - assets/bundled/prefabs/ui/lootpanels/lootpanel.repairbench.prefab
        896 - assets/bundled/prefabs/ui/lootpanels/lootpanel.researchtable.prefab
        897 - assets/bundled/prefabs/ui/lootpanels/lootpanel.smallrefinery.prefab
        898 - assets/bundled/prefabs/ui/lootpanels/lootpanel.smallstash.prefab
        899 - assets/bundled/prefabs/ui/lootpanels/lootpanel.smallwoodbox.prefab
        900 - assets/bundled/prefabs/ui/lootpanels/lootpanel.watercatcher.prefab
         */

        [ChatCommand("backpack")]
        BaseEntity SpawnBackpack(BasePlayer player)
        {
            //string prefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
            string prefab = "assets/prefabs/deployable/small stash/small_stash_deployed.prefab";
            StorageContainer entity = (StorageContainer)GameManager.server.CreateEntity(prefab);
            if (entity != null)
            {
                backpackNetworking.Add(entity, player);
                if (entity.children != null)
                {
                    foreach (BaseEntity child in entity.children)
                    {
                        Puts("Child entity");
                        backpackNetworking.Add(entity, player);
                    }
                }
                entity.Spawn();
                entity.SetParent(player, "spine1");
                if (MyBackpackComponent.positionSettings.ContainsKey(prefab))
                {
                    entity.transform.position = MyBackpackComponent.positionSettings[prefab].offset;
                    entity.transform.rotation = Quaternion.Euler(MyBackpackComponent.positionSettings[prefab].rotation);
                }

                var colliders = entity.GetComponents<Collider>();
                foreach (Collider collider in colliders)
                {
                    //entity.gameObject.compon(collider); 
                }

                Puts(entity.GetComponentsInParent<Collider>().Length.ToString());
                entity.SendNetworkUpdate();
                fakeEntities.Add(entity);
                var backPack = player.GetComponent<MyBackpackComponent>();
                if (backPack == null)
                {
                    backPack = player.gameObject.AddComponent<MyBackpackComponent>();
                    backPack.container = entity;
                    backPack.lootPanel = "smallstash";
                    backpackData.Add(player, backPack);
                }
            }
            return entity;
        }

        void OnTick()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                if (backpackData.ContainsKey(player))
                {
                    backpackData[player].backpackOpen = (bool)player?.inventory?.loot?.IsLooting();
                }
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            player.SendConsoleCommand("bind tab inventory.toggle;backpackInvButton");
        }

        [ConsoleCommand("backpackInvButton")]
        void InventoryButtonPressed(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player.IsSleeping())
            {
                return;
            }
            if ((bool)player.inventory.loot.IsLooting())
            {
                Puts("Is looting");
                return;
            }
            OpenBackpack(player);
        }

        bool OpenBackpack(BasePlayer player)
        {
            var backpack = player.GetComponent<MyBackpackComponent>();
            if (backpack == null || player.inventory == null)
            {
                Puts("No backpack");
                return false;
            }
            if (!backpackData.ContainsKey(player))
            {
                Puts("Backpack data doesn't contain player!");
                return false;
            }
            if (backpackData[player].backpackOpen)
            {
                return false;
            }

            player.inventory.loot.StartLootingEntity(backpack.container,false);
            player.inventory.loot.AddContainer(backpack.container.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", backpack.lootPanel, null, null, null, null);
            backpack.backpackOpen = true;
            return true;
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            Puts("OnLootEntity works!");
        }

        [ChatCommand("player")]
        BasePlayer SpawnPlayer(BasePlayer player)
        {
            string prefab = "assets/prefabs/player/player.prefab";
            BasePlayer newPlayer = (BasePlayer)GameManager.server.CreateEntity(prefab, player.transform.position + new Vector3(0, 0, 1));
            newPlayer.Spawn();
            //newPlayer.InitializeHealth(1000, 1000); 
            newPlayer.Heal(100);
            fakePlayers.Add(newPlayer);
            SpawnBackpack(newPlayer);
            return newPlayer;
        }

        public class MyBackpackComponent : MonoBehaviour
        {
            public static Dictionary<string, MyPositionConfig> positionSettings = new Dictionary<string, MyPositionConfig>()
            {
                {"assets/prefabs/deployable/small stash/small_stash_deployed.prefab", new MyPositionConfig(new Vector3(-0.25f,-0.15f,0f),new Vector3(180,270,0)) },


            };

            public StorageContainer container { get; set; }
            public string lootPanel { get; set; }
            public bool backpackOpen { get; set; }

            public MyBackpackComponent()
            {
                container = new StorageContainer();
            }
        }

        public class MyPositionConfig
        {
            public Vector3 offset { get; set; }
            public Vector3 rotation { get; set; }

            public MyPositionConfig(Vector3 position, Vector3 rot)
            {
                offset = position;
                rotation = rot;
            }
        }
    }
}


