using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Plugins;

namespace Oxide.Plugins
{
    [Info("PersistanceDrops", "Jake_Rich", 0.1)]
    [Description("Item's dropped on ground won't despawn, without lagging the client")]

    public class PersistentDrops : RustPlugin
    {
        public static PersistentDrops thisPlugin;

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

        public static Dictionary<Vector2, GridData> itemGrid = new Dictionary<Vector2, GridData>();

        void Loaded()
        {
            thisPlugin = this;
        }

        void Unload()
        {

        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            //Puts(entity.GetType().ToString());
            if (entity.GetType() == typeof(DroppedItem))
            {
                DroppedItem item = (DroppedItem)(entity);
                //Puts(item.gameObject);
                //Puts(BasePlayer.activePlayerList[0].GetActiveItem()?.info.worldModelPrefab.resourcePath.ToString());
            }
            
            //Puts("OnEntitySpawned works!");
        }



    }
}

public class GridData
{
    public HashSet<BasePlayer> subscribedPlayers = new HashSet<BasePlayer>();
    public HashSet<BaseEntity> items = new HashSet<BaseEntity>();
    public int versionNumber = 0;
    public float timeCreated;

}



