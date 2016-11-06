using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Plugins;

namespace Oxide.Plugins
{
    [Info("JakePlugin", "Jake_Rich", 0.1)]
    [Description("Makes stuff happen")]

    public class JakePlugin : RustPlugin
    {
        public static JakePlugin thisPlugin;

        #region Settings
        int tickLimit { get; set; } = 2000;
        int tickRemoveAmount { get; set; } = 500;
        #endregion

        #region Loading
        void Loaded()
        {
            thisPlugin = this;
            //InitFloorIDs();
            //InitHiddenRooms();
        }

        void Unload()
        {
            Write("Active players", BasePlayer.activePlayerList.Count);
            foreach(BasePlayer player in fakePlayers)
            {
                customData.Remove(player.userID);
                player.Kill();
            }
            foreach (BaseNetworkable item in fakeEntities)
            {
                item.Kill();
            }
        }
        #endregion

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

        public static Dictionary<ulong, CustomPlayerData> customData = new Dictionary<ulong, CustomPlayerData>();
        public static List<BasePlayer> fakePlayers = new List<BasePlayer>();    
        public static List<BaseNetworkable> fakeEntities = new List<BaseNetworkable>();

        #region ServerTick
        int tickCount = 0;
        int everyOtherTick = 0;
        //Default 10 calls per second
        void OnTick()
        {
            tickCount++;
            if (tickCount > 100) //Cull playerticks every 10 seconds
            {
                tickCount = 0;
                foreach (CustomPlayerData data in customData.Values)
                {
                    data.tickData.CullTicks(tickRemoveAmount, tickLimit);
                }
                if (TOD_Sky.Instance.Cycle.Hour > 14 || TOD_Sky.Instance.Cycle.Hour < 10)
                {
                    TOD_Sky.Instance.Cycle.Hour = 10;
                }
            }

            foreach(KeyValuePair<ulong,CustomPlayerData> data in customData)
            {
                data.Value.tickData.AnalyzeShots();
            }

            if (everyOtherTick >= 1)
            {
                everyOtherTick = 0;
                //Write("Players connected:",customData.Count);
                foreach (CustomPlayerData data in customData.Values)
                {
                    data.playerCull.Update();
                    //Write(data.playerCull.playersToCheck.Count);
                }
            }
            everyOtherTick++;
            /*
            foreach (BasePlayer player in fakePlayers)
            {
                player.ShouldNetworkTo(BasePlayer.activePlayerList[0]);
            }*/
        }
        #endregion

        #region Tick Storage
        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        { 
            if (customData.ContainsKey(player.userID))
            {
                for (int i = 0; i < projectiles.projectiles.Count; i++)
                {
                    customData[player.userID].tickData.AddShootTick(projectiles.projectiles[i], projectile, projectiles.projectiles[i]);
                }
            }
            projectile.primaryMagazine.contents = 100;
            projectile.SendNetworkUpdate();
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!customData.ContainsKey(player.userID))
            {
                customData.Add(player.userID, new CustomPlayerData(player));
            }
            customData[player.userID].tickData.AddTick(input.current.aimAngles);
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            TickData tick = customData[attacker.userID].tickData;
            if (tick.shootTicks.ContainsKey(info.ProjectileID))
            {
                tick.shootTicks[info.ProjectileID].hitInfo = info;
            }

        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            customData.Remove(player.userID);
        }

        #endregion

        #region Misc Test
        [ChatCommand("spawn")]
        void TestCommand(BasePlayer player, string command, string[] args)
        {
            string prefab = "assets/prefabs/player/player.prefab";
            BasePlayer newPlayer = (BasePlayer)GameManager.server.CreateEntity(prefab,player.transform.position);
            newPlayer.Spawn();
            newPlayer.InitializeHealth(1000, 1000);
            newPlayer.StartMaxHealth();
            fakePlayers.Add(newPlayer);
            Network.Connection connection = new Network.Connection();
            connection.player = newPlayer;
            connection.userid = newPlayer.userID;
            connection.username = player.displayName;
            newPlayer.PlayerInit(connection);
        }

        [ChatCommand("fake")]
        void SpawnPlayersTest(BasePlayer player, string command, string[] args)
        {
            int amount = 0;
            if (args.Length == 1)
            {
                amount = int.Parse(args[0]);
            }
            int rows = (int)Mathf.Sqrt(amount);
            float seperationDistance = 5;
            int count = 0;

            for (int x = 0; x < rows; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    string prefab = "assets/prefabs/player/player.prefab";

                    BasePlayer newPlayer = (BasePlayer)GameManager.server.CreateEntity(prefab, player.transform.position + new Vector3(seperationDistance * x, 30, seperationDistance * y));
                    newPlayer.Spawn();
                    newPlayer.InitializeHealth(1000, 1000);
                    newPlayer.StartMaxHealth();
                    fakePlayers.Add(newPlayer);
                    customData.Add(newPlayer.userID, new CustomPlayerData(newPlayer));
                    if (count > amount)
                    {
                        return;
                    }
                    count++;
                }
            }
        }

        bool god = true;
        [ChatCommand("god")]
        void ToggleGod(BasePlayer player, string command, string[] args)
        {
            god = !god;
        }
        #endregion

        #region Unlimited Ammo / Godmode

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (god)
            {
                info.damageTypes.ScaleAll(0f);
            }
        }

        void OnLoseCondition(Item item, ref float amount)
        {
            item.condition = item.maxCondition;
        }

        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            var weapon = player.GetActiveItem().GetHeldEntity() as BaseProjectile;
            if (weapon == null) return;
            player.GetActiveItem().condition = player.GetActiveItem().info.condition.max;
            if (weapon.primaryMagazine.contents > 0) return;
            weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
            weapon.SendNetworkUpdateImmediate();
        }

        #endregion

        object CanNetworkTo(BaseNetworkable entity, BasePlayer player)
        {
            if (entity.GetType() == typeof(BasePlayer)) //Trying to network one player to another player
            {
                BasePlayer targetPlayer = (BasePlayer)entity;
                if (customData.ContainsKey(targetPlayer.userID))
                {
                    PlayerCulling culling = customData[player.userID].playerCull;
                    //Write(string.Format("testing ShouldNetwork: {1} --> {0} : {2}", player.displayName, targetPlayer.displayName, culling.ShouldShow(targetPlayer).ToString()));
                    return true;
                    //return culling.ShouldShow(targetPlayer);
                }
            }
            return null;
        }

        #region HiddenRooms

        Dictionary<uint, MyRoom> individualRooms = new Dictionary<uint, MyRoom>();
        HashSet<CompletedRoom> completeRooms = new HashSet<CompletedRoom>();
        static HashSet<string> floorPrefabNames = new HashSet<string>();


        #region Settings
        const int maxStackedItems = 10;
        #endregion

        void InitFloorIDs()
        {
            floorPrefabNames.Add("foundation");
            floorPrefabNames.Add("foundation.triangle");
            floorPrefabNames.Add("floor");
            floorPrefabNames.Add("floor.triangle"); 
        }

        void InitHiddenRooms()
        {
            foreach (BaseEntity item in GameObject.FindObjectsOfType<BaseEntity>())
            {
                if (true)
                {
                    //JakePlugin.Write(string.Format("{0} Found, has {1}", item.ShortPrefabName, item.GetEntityLinks(false).Count));
                }
            }
            foreach (BuildingBlock floor in GameObject.FindObjectsOfType<BuildingBlock>())
            {
                //JakePlugin.Write(floor.ShortPrefabName);
                if (floorPrefabNames.Contains(floor.ShortPrefabName))
                {
                    MyRoom room = new MyRoom();
                    room.floor = floor;
                    Stack<BaseEntity> queueToCheck = new Stack<BaseEntity>();
                    queueToCheck.Push(floor);
                    floor.gameObject.transform.DestroyAllChildren();
                    //JakePlugin.Write(string.Format("{0} has {1} entity links", floor.ShortPrefabName, ));
                    if (floor.children != null)
                    {
                        JakePlugin.Write(string.Format("{1} has {0} children", floor.children.Count, floor.ShortPrefabName));
                    }
                    while (queueToCheck.Count > 0)
                    {
                        BaseEntity current = queueToCheck.Pop();
                        if (current.children != null)
                        {
                            foreach (BaseEntity item in current.children)
                            {
                                queueToCheck.Push(item);
                                room.contents.Add(item);
                            }
                        }
                    }
                    if (!individualRooms.ContainsKey(room.floor.net.ID))
                    {
                        individualRooms.Add(room.floor.net.ID, room);
                    }
                    else
                    {
                        JakePlugin.Write("Room already exists!");
                    }
                    
                    JakePlugin.Write(string.Format("Room has {0} children",room.contents.Count));
                }
            }
            JakePlugin.Write(string.Format("{0} rooms", individualRooms.Count));
            InitConnectRooms();
        }

        void InitConnectRooms()
        {
            foreach(MyRoom room in individualRooms.Values)
            {
                if (room.connectedRooms.rooms == null)
                {
                    room.connectedRooms.rooms = new HashSet<MyRoom>();
                    room.connectedRooms.rooms.Add(room);
                }
                List<EntityLink> entityLinks = room.floor.GetEntityLinks(true);
                //JakePlugin.Write("Entity Links:", entityLinks.Count);
                for (int i = 0; i < entityLinks.Count; i++)
                {
                    //JakePlugin.Write(entityLinks[i].name);
                }
                if (entityLinks.Count == 9) //Triangle
                { 
                    for (int i = 0; i < 3; i++)
                    {
                        //JakePlugin.Write(entityLinks[i].name);
                        if (entityLinks[i + 6].IsEmpty()) //Wall is empty
                        {
                            if (entityLinks[i].IsOccupied())//Foundation Is attached
                            {
                                BuildingBlock connectedFloor = entityLinks[i].connections[0].owner as BuildingBlock;
                                MyRoom adjacentRoom = individualRooms[connectedFloor.net.ID];
                                if (adjacentRoom.connectedRooms.rooms != null)
                                {
                                    room.connectedRooms.rooms.UnionWith(adjacentRoom.connectedRooms.rooms);
                                    adjacentRoom.connectedRooms = room.connectedRooms;
                                }
                                else
                                {
                                    room.connectedRooms.rooms.Add(adjacentRoom);
                                    adjacentRoom.connectedRooms = room.connectedRooms;
                                }
                            }
                            else
                            {
                                room.connectedRooms.visibleOutside = true;
                            }
                        }
                        //JakePlugin.Write(string.Format("Slot {0} empty, {1}", i, entityLinks[i].gender));
                    }
                }
            }

            foreach(MyRoom room in individualRooms.Values)
            {
                JakePlugin.Write(room.connectedRooms.rooms.Count, "rooms connected");
                completeRooms.Add(room.connectedRooms);
            }

            int count = 0;
            foreach (CompletedRoom room in completeRooms)
            {
                count++;
                if (room.visibleOutside)
                {
                    JakePlugin.Write("Room visible outside");
                }
                else
                {
                    JakePlugin.Write("Room invisible");
                }
            }

            JakePlugin.Write(completeRooms.Count, "individual rooms");
        }

        HashSet<MyRoom> GetConnectedRooms()
        {
            HashSet<MyRoom> returnRooms = new HashSet<MyRoom>();

            return returnRooms;
        }

        bool ShouldNetworkBuildingItem(BaseEntity entity, BasePlayer player)
        {
            if (entity != null)
            {
                int loopCount = 0;
                BaseNetworkable parent = BaseNetworkable.serverEntities.Find(entity.parentEntity.uid);
                while (loopCount <= maxStackedItems)
                {
                    loopCount++;//In real rust, each entity would have reference to main parent, instead of this loop. Maybe a bool to say if it is a floor / foundation.
                    if (parent != null)
                    {
                        if (parent.GetType() == typeof(BaseEntity))
                        {
                            BaseNetworkable newParent = BaseNetworkable.serverEntities.Find(((BaseEntity)parent).parentEntity.uid);
                            {
                                parent = newParent;
                                continue;
                            }
                        }
                        //We have reached main parent
                        if (IsBuildingBlock(parent))
                        {
                            BuildingBlock buildingBlock = (BuildingBlock)parent;
                            if (floorPrefabNames.Contains(buildingBlock.blockDefinition.hierachyName))
                            {
                                if (individualRooms.ContainsKey(buildingBlock.net.ID))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
            return true;
        }

        bool IsBuildingBlock(BaseNetworkable entity)
        {
            return entity.GetType() == typeof(BuildingBlock);
        }
        #endregion

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            
            if (info.HitEntity == null)
            {
                Puts("Null hit entity from hammer");
                return;
            }

            foreach (EntityLink link in info.HitEntity.GetEntityLinks())
            {
                for (int i = 0; i < link.connections.Count; i++)
                {
                    if (link.connections[i].owner != null)
                    {
                        player.SendConsoleCommand("ddraw.box", 3f, Color.red, link.connections[i].owner?.transform.position);
                    }
                }
                
            }

            return;
            if (info.HitEntity != null)
            {
                SpawnEntity(info.HitPositionWorld, player);
                if (info.HitEntity.GetType() == typeof(BoxStorage))
                {
                    BoxStorage entity = (BoxStorage)info.HitEntity;
                    Write(entity._collider);
                }
               
            }
        }

        void SpawnEntity(Vector3 pos, BasePlayer player)
        {
            //string prefab = "assets/prefabs/weapons/ak47u/ak47u.entity.prefab"; 
            string prefab = "assets/prefabs/ammo/arrow/arrow.prefab";
            //BaseEntity bear = SpawnAsBear(player);
            BaseEntity entity = GameManager.server.CreateEntity(prefab);
            if (entity != null)
            {
                //entity.transform.localPosition = new Vector3(0, 5, 0);
                entity.Spawn();
                for (int i = 0; i < player.skeletonProperties.bones.Length - 30; i++)
                {
                    JakePlugin.Write(player.skeletonProperties.bones[i].bone.name);
                }
                entity.SetParent(player, "RustPlayer");
                //entity.SetParent(bear, "RustPlayer");
                entity.transform.position = new Vector3(0, 0, 0);
                entity.transform.rotation = Quaternion.Euler(0, 0, 0);
                entity.SendNetworkUpdateImmediate();
                fakeEntities.Add(entity);
                JakePlugin.Write("Object spawned");
            }
            //BaseNetworkable entity = GameManager.server.CreateEntity(prefab, parent:player.transform);
        }
        
        [ChatCommand("bear")]
        BaseEntity SpawnAsBear(BasePlayer player)
        {
            string prefab = "assets/bundled/prefabs/autospawn/animals/bear.prefab";
            BaseEntity entity = GameManager.server.CreateEntity(prefab);
            if (entity != null)
            {
                entity.Spawn();
                entity.SetParent(player, "RustPlayer");
                entity.transform.position = new Vector3(0, 0, 0);
                entity.transform.rotation = Quaternion.Euler(0, 0, 0);
                entity.SendNetworkUpdate();
                fakeEntities.Add(entity);
                JakePlugin.Write("Object spawned");
            }
            return entity;
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            BaseEntity entity = go.GetComponent<BaseEntity>();
            if (entity != null)
            {
                if (entity.ShortPrefabName == "stocking_small_deployed")
                {

                }
                //if (entity.PrefabName ==)
            }
        }

        [ChatCommand("pr")]
        BaseEntity TestPrefab(BasePlayer player)
        {
            //string prefab = "assets/prefabs/deployable/covers/cover_b.prefab";
            string prefab = "assets/prefabs/tools/flashlight/flashlight.weapon.prefab";
            BaseEntity entity = GameManager.server.CreateEntity(prefab, player.transform.position);
            if (entity != null)
            {
                entity.Spawn();
                //entity.SetParent(player, "RustPlayer");
                entity.transform.position = player.transform.position;
                //entity.transform.position = player.transform.position;
                entity.transform.rotation = Quaternion.Euler(0, 0, 0);
                entity.SendNetworkUpdate();
                fakeEntities.Add(entity);
                JakePlugin.Write("Object spawned");
            }
            return entity;
        }

        [ChatCommand("tr")]
        void TestPrefabs(BasePlayer player)
        {
            //string prefab = "assets/prefabs/deployable/covers/cover_b.prefab";
            string prefab = "assets/bundled/prefabs/system/debug/debug_camera.prefab";
            GameObject obj = GameManager.server.CreatePrefab(prefab);
            obj.transform.position = player.transform.position;
            //obj.transform.SetParent(player.transform);
            foreach(Component comp in obj.GetComponents<Component>())
            {
                JakePlugin.Write(comp);
            }
            /*BaseNetworkable entity = 
            if (entity != null)
            {
                entity.Spawn();
                entity.SetParent(player, "RustPlayer");
                entity.transform.position = new Vector3(0, 0, 0);
                BuildingBlock block = (BuildingBlock)entity;
                //entity.transform.position = player.transform.position;
                entity.transform.rotation = Quaternion.Euler(0, 0, 0);
                //entity.SendNetworkUpdate();
                fakeEntities.Add(entity);
                JakePlugin.Write("Object spawned");
            }
            return entity;*/
        }

    }

}

public class MyRoom
{
    public BuildingBlock floor;
    public HashSet<BaseEntity> contents = new HashSet<BaseEntity>();
    public CompletedRoom connectedRooms = new CompletedRoom();
    public bool visibleOutside { get; set; }

}

public class CompletedRoom
{
    public HashSet<MyRoom> rooms;
    public bool visibleOutside { get; set; } = false;
}

public class CustomPlayerData
{
    public TickData tickData { get; set; }
    public PlayerCulling playerCull { get; set; }

    public CustomPlayerData(BasePlayer player)
    {
        tickData = new TickData(player);
        playerCull = new PlayerCulling(player);

    }
}

#region PlayerCulling
public class PlayerCulling
{

    #region Settings
    public static float maxPlayerDist { get; set; } = 5000f;
    public static float minCullDist { get; set; } = 20f;
    public static float timeVisableAfterSeen { get; set; } = 10f;
    public static float maxSleeperDist { get; set; } = 30f;
    public static int visQuality { get; set; } = 2;
    public static float timeStep = 0.2f;
    public static float timeToUpdateSleepers { get; set; } = 1f;
    #endregion

    #region Properties
    public float nextVisThink { get; set; }
    public bool NeedsUpdate { get { return Time.realtimeSinceStartup >= nextVisThink; } }
    public List<Network.Connection> groupPlayerList
    {
        get
        {
            return player.net.group.subscribers;
        }
    }
    public HashSet<Network.Networkable> playerItemList
    {
        get
        {
            return player.net.group.networkables;
        }
    }
    public HashSet<BasePlayer> visiblePlayerList { get; set; } = new HashSet<BasePlayer>();
    public Dictionary<BasePlayer, float> nextTimeToCheck = new Dictionary<BasePlayer, float>();
    public HashSet<BasePlayer> playersToCheck { get; set; } = new HashSet<BasePlayer>();
    public BasePlayer player { get; set; }
    #endregion

    #region Constructor
    public PlayerCulling(BasePlayer player)
    {
        this.player = player;
    }
    #endregion

    public void Update()
    {
        if (player == null)
        {
            JakePlugin.Write("PlayerNull, returning");
            return;
        }

        //JakePlugin.Write(player.displayName, ", items in group:", groupPlayerList.Count);
        //JakePlugin.Write(player.userID, ", players to check:", playersToCheck.Count);

        /*
        for (int i = 0; i < groupPlayerList.Count; i++)
        {*/
        for (int i = 0; i < JakePlugin.fakePlayers.Count; i++)
        {
            //PlayerCulling targetPlayerCulling = JakePlugin.customData[groupPlayerList[i].userid].playerCull;
            PlayerCulling targetPlayerCulling = JakePlugin.customData[JakePlugin.fakePlayers[i].userID].playerCull;
            BasePlayer playerToCheck = targetPlayerCulling.player;
            if (playerToCheck == player)
            {
                continue;
            }
            if (JakePlugin.customData.ContainsKey(playerToCheck.userID))
            {
                if (targetPlayerCulling.playersToCheck.Contains(player))
                {
                    if (playersToCheck.Contains(playerToCheck))
                    {
                        playersToCheck.Remove(playerToCheck);
                        //JakePlugin.Write("ERROR: Players set to check eachother");
                    }
                    continue;
                }
                else
                {
                    playersToCheck.Add(playerToCheck);
                }
                
                if (!nextTimeToCheck.ContainsKey(playerToCheck))
                {
                    nextTimeToCheck.Add(playerToCheck, Time.realtimeSinceStartup + PlayerCulling.timeStep);
                }

                if (nextTimeToCheck[playerToCheck] >= Time.realtimeSinceStartup) { continue; }
                
                //JakePlugin.Write(string.Format("{0}: Players to check: {1}", player.displayName, playersToCheck.Count));

                targetPlayerCulling.playersToCheck.Add(player);
                float num = Vector3.Distance(player.eyes.position, playerToCheck.transform.position);
                bool shouldShow = false;
                if (playerToCheck.IsSleeping() && num > PlayerCulling.maxSleeperDist)
                {
                    JakePlugin.Write("Player sleeping: won't show");
                    shouldShow = false;
                }
                else if (num > PlayerCulling.maxPlayerDist)
                {
                    JakePlugin.Write("Player above max distance: won't show");
                    shouldShow = false;
                }
                else if (num <= PlayerCulling.minCullDist)
                {
                    JakePlugin.Write(playerToCheck.displayName, player.displayName);
                    JakePlugin.Write("Player below min distance: will show");
                    shouldShow = true;
                }
                else if (IsAimingAt(player, playerToCheck, 0.99f))
                {
                    shouldShow = true;
                }
                else if (IsAimingAt(playerToCheck, player, 0.99f))
                {
                    shouldShow = true;
                }
                else
                {
                    Vector3 normalized = (player.eyes.position - player.eyes.position).normalized;
                    float num2 = Vector3.Dot(player.eyes.HeadForward(), normalized);
                    shouldShow = (num2 >= 0f && AnyPartVisible(player, playerToCheck));
                    //JakePlugin.Write("Playercull Update:", shouldShow);
                }
                PlayerCulling targetCulling = JakePlugin.customData[playerToCheck.userID].playerCull;
                if (shouldShow)
                {
                    nextTimeToCheck[playerToCheck] = Time.realtimeSinceStartup + PlayerCulling.timeVisableAfterSeen;
                    targetCulling.visiblePlayerList.Add(player);
                    visiblePlayerList.Add(playerToCheck);
                }
                else
                {
                    if (playerToCheck.IsSleeping())
                    {
                        nextTimeToCheck[playerToCheck] = Time.realtimeSinceStartup + PlayerCulling.timeToUpdateSleepers;
                    }
                    targetCulling.visiblePlayerList.Remove(player);
                    visiblePlayerList.Remove(playerToCheck);
                }
            }
        }
    }

    public bool ShouldShow(BasePlayer targetPlayer)
    {
        return visiblePlayerList.Contains(targetPlayer);
    }

    #region Playerculling functions from client

    private bool VisPlayerArmed(BasePlayer player)
    {
        HeldEntity heldEntity = player.GetHeldEntity();
        return heldEntity != null && heldEntity is BaseProjectile;
    }

    public bool IsAimingAt(BasePlayer aimer, BasePlayer target, float cone = 0.95f)
    {
        Vector3 normalized = (target.eyes.position - aimer.eyes.position).normalized;
        float num = Vector3.Dot(aimer.eyes.HeadForward(), normalized);
        return num > cone && VisPlayerArmed(aimer) && VisPlayerArmed(target);
    }

    private bool AnyPartVisible(BasePlayer localPlayer, BasePlayer targetPlayer)
    {
        Vector3 position = localPlayer.eyes.position;
        Vector3 a = localPlayer.eyes.HeadRight();
        Vector3 vector = targetPlayer.CenterPoint();
        if (targetPlayer.IsSleeping())
        {
            vector += new Vector3(0f, 1f, 0f);
        }
        float dist = Vector3.Distance(position, vector);
        bool flag = this.PointSeePoint(position, vector, dist, true);
        if (targetPlayer.IsSleeping())
        {
            return flag;
        }
        if (!flag && PlayerCulling.visQuality > 0)
        {
            Vector3 position2 = targetPlayer.eyes.position;
            flag = PointSeePoint(position, position2, dist, false);
        }
        if (!flag && PlayerCulling.visQuality > 1)
        {
            Vector3 origin = vector + a * 0.25f;
            flag = PointSeePoint(position, origin, dist, false);
            if (!flag)
            {
                Vector3 origin2 = vector + a * -0.25f;
                flag = PointSeePoint(position, origin2, dist, false);
            }
        }
        if (!flag && PlayerCulling.visQuality > 2)
        {
            flag = PointSeePoint(position, targetPlayer.transform.position + new Vector3(0f, 0.1f, 0f), dist, false);
        }
        return flag;
    }

    public bool PointSeePoint(Vector3 target, Vector3 origin, float dist = 0f, bool useGameTrace = false)
    {
        bool flag = false;
        if (dist == 0f)
        {
            dist = Vector3.Distance(target, origin);
        }
        Vector3 normalized = (target - origin).normalized;
        Ray ray = new Ray(origin, normalized);
        RaycastHit raycastHit;
        if ((!useGameTrace) ? Physics.Raycast(ray, out raycastHit, dist, 10551297) : GamePhysics.Trace(ray, 0f, out raycastHit, dist, 10551297, QueryTriggerInteraction.UseGlobal))
        {
            ColliderInfo component = raycastHit.collider.GetComponent<ColliderInfo>();
            if (component == null || component.HasFlag(ColliderInfo.Flags.VisBlocking))
            {
                flag = true;
            }
        }
        return !flag;
    }

    #endregion

}

public class PlayerCullingSettings
{
    public BasePlayer targetPlayer;
    public float nextVisThink { get; set; }
    public bool NeedsUpdate { get { return Time.realtimeSinceStartup >= nextVisThink; } }
    public bool ShouldShow { get; set; }

    public PlayerCullingSettings(BasePlayer targetPlayer)
    {
        this.targetPlayer = targetPlayer;
    }

    public void MarkVisible()
    {
        nextVisThink = Time.realtimeSinceStartup + PlayerCulling.timeVisableAfterSeen;
    }

}

#endregion

#region Anti-Aimbot

//Need to make sure this is thread safe, so it can run on a seperate thread, and not fuck up when shoot ticks are added / removed
public class TickData
{
    public List<Vector2> storedTicks { get; set; } = new List<Vector2>();
    public Dictionary<int, ShootTick> shootTicks { get; set; } = new Dictionary<int, ShootTick>();
    uint missedProjectileIDs { get; set; }
    int currentTick { get { return storedTicks.Count - 1; }}
    float timeLastShotFired { get; set; }
    BasePlayer player;
    public int noSpreadViolations { get; set; }
    public float recoilViolationLevel { get; set; }
    private int concurrentRecoilViolations { get; set; } = 0;


    public TickData(BasePlayer player)
    {
        this.player = player;
    }

    public void CullTicks(int amount, int limit)
    {
        if (storedTicks.Count > limit)
        {
            storedTicks.RemoveRange(0, amount);
            List<int> ticksToRemove = new List<int>();
            foreach (KeyValuePair<int, ShootTick> tick in shootTicks)
            {
                tick.Value.tickFired -= amount;
                if (tick.Value.tickFired < 0)
                {
                    ticksToRemove.Add(tick.Key);
                }
            }
            for (int i = 0; i < ticksToRemove.Count; i++)
            {
                shootTicks.Remove(ticksToRemove[i]);
            }
        }

    }

    public void AddTick(Vector2 aimDirection)
    {
        if (storedTicks != null)
        {
            storedTicks.Add(aimDirection);
        }
    }

    public void AddShootTick(ProtoBuf.ProjectileShoot.Projectile firedProjectile,BaseProjectile projectile, ProtoBuf.ProjectileShoot.Projectile bullet)
    {
        ShootTick tick = new ShootTick();
        tick.tickFired = currentTick;
        tick.timeDelta = Time.realtimeSinceStartup - timeLastShotFired;
        tick.projectile = projectile;
        timeLastShotFired = Time.realtimeSinceStartup;
        tick.bullet = bullet;
        shootTicks.Add(firedProjectile.projectileID, tick);
    }

    public Vector2 GetRecoilChange(ShootTick tick)
    {
        Vector2 originalAimPos = storedTicks[tick.tickFired];
        int ticksAhead = 0;
        //JakePlugin.Write(string.Format("Recoil Yaw Min:{0} Yaw Max:{1} Pitch Min: {2} Pitch Max: {3}", tick.projectile.recoil.recoilYawMin, tick.projectile.recoil.recoilYawMax, tick.projectile.recoil.recoilPitchMin, tick.projectile.recoil.recoilPitchMax, tick.projectile.recoil.recoilPitchMax));
        while (tick.tickFired < storedTicks.Count)
        {
            ticksAhead++;
            if (ticksAhead >= 4)
            {
                Vector2 returnVector = storedTicks[tick.tickFired] - originalAimPos;
                returnVector = new Vector2(Mathf.Abs(returnVector.y), Mathf.Abs(returnVector.x));
                return returnVector;
            }
            tick.tickFired++;
        }
        return new Vector2(float.MaxValue, float.MaxValue);
    } //Both functions should return float from 0 - 100f+, indicating violation level

    public void UpdateRecoilViolation(Vector2 recoilAmount,BaseProjectile weapon)
    {
        RecoilProperties recoil = weapon.recoil;
        Vector2 obviousThreshold = new Vector2(Mathf.Abs(recoil.recoilYawMin - recoil.recoilYawMax), Mathf.Abs(recoil.recoilPitchMax) * 2f);
        if (!(recoil.recoilYawMin < 0 && recoil.recoilYawMax > 0)) //If recoil is only one direction, make limit harder to hit (only horizontal)
        {
            obviousThreshold.Set(obviousThreshold.x * 3f, obviousThreshold.y);
        }
        obviousThreshold /= 350f; //Around 10f y, and 
        //JakePlugin.Write("Obvious threshold", obviousThreshold * 1000f);
        JakePlugin.Write(recoilAmount - obviousThreshold * 1000f, "above threshold");
        if (recoilAmount.x <= obviousThreshold.x && recoilAmount.y <= obviousThreshold.y) //Absolute obvious recoil, accounts for sway
        {
            concurrentRecoilViolations++;
            recoilViolationLevel += (concurrentRecoilViolations - 1) * 500f + 100f; //If there is a one off violation, will add a bit
            JakePlugin.Write("Recoil Violation UPDATED ------------->", recoilViolationLevel);
        }
        obviousThreshold *= 5f;


    }

    public float GetSpreadChange(ShootTick tick)
    {
        Quaternion eyeDirection = Quaternion.Euler(storedTicks[tick.tickFired + 1]);
        Quaternion bulletRotation = Quaternion.LookRotation(tick.bullet.startVel);
        return Vector3.Angle(eyeDirection * Vector3.forward, tick.bullet.startVel.normalized);
    }

    public float GetPathChange(ShootTick tick)
    {
        float pathChange = 0f;
        Vector2 lastPos = storedTicks[tick.tickFired - 20];
        for (int i = tick.tickFired - 19; i < tick.tickFired; i++)
        {
            Mathf.Abs(Vector2.Angle(storedTicks[i], lastPos));
            lastPos = storedTicks[i];
        }
        return pathChange;
    }

    public float CheckBoneSnapping(ShootTick tick)
    {
        float distanceOff = 0f;
        if (tick.hitInfo == null)
        {
            JakePlugin.Write("Null Tick for boneSnapping");
            return float.MaxValue;
        }
        if (tick.hitInfo.HitEntity != null)
        {
            if (tick.hitPlayer)
            {
                BasePlayer player = (BasePlayer)tick.hitInfo.HitEntity;
                Vector3 bonePos = player.skeletonProperties.FindBone(tick.hitInfo.HitBone).bone.transform.position;
                Vector3 boneWorldPos = bonePos + player.transform.position;
                JakePlugin.Write(tick.hitInfo.HitBone,bonePos);
            }
        }
        return distanceOff;
    }

    public void AnalyzeShots()
    {
        List<int> toRemove = new List<int>();
        foreach (KeyValuePair<int, ShootTick> tick in shootTicks)
        {
            if (currentTick > tick.Value.tickFired + 50)
            {
                Vector2 recoilChange = GetRecoilChange(tick.Value) * 1000;
                //JakePlugin.Write("Recoil:", recoilChange);
                UpdateRecoilViolation(recoilChange,tick.Value.projectile);
                GetSpreadChange(tick.Value);
                CheckBoneSnapping(tick.Value);

                toRemove.Add(tick.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            shootTicks.Remove(toRemove[i]);
        }
    }
}

public class ShootTick
{
    public bool hitPlayer
    {
        get
        {
            if (hitInfo != null)
            {
                return hitInfo.HitEntity.GetType() == typeof(BasePlayer);
            }
            return false;
        }
    }
    public BaseProjectile projectile { get; set; }
    public ProtoBuf.ProjectileShoot.Projectile bullet { get; set; }
    public HitInfo hitInfo { get; set; }
    public int tickFired { get; set; } = 0;
    public float timeDelta { get; set; }
}
#endregion