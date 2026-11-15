var LOOK = Substrate.Sectors.Elements.Stations.Visual.StationLook.__LOOK__;
var go = UnityEngine.GameObject.Find("STATION_PROOF");
if (!go) return "no station";
go.GetComponent<Substrate.Sectors.Elements.Stations.Visual.StationMotion>().Show(LOOK);
return "look " + LOOK;
