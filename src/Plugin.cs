using System;
using BepInEx;
using UnityEngine;
using Smoke;
using ImprovedInput;
using DressMySlugcat;
using static MonoMod.InlineRT.MonoModRule;
using HUD;
using RWCustom;
using UnityEngine.Assertions.Must;
using JetBrains.Annotations;
using System.Configuration;
using System.Runtime.ConstrainedExecution;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using MoreSlugcats;
using SlugBase.SaveData;
using SlugBase.Features;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "CarlCat", "0.1.0")]
    class Plugin : BaseUnityPlugin
    {
        private const string MOD_ID = "bvipri.carlcat";
        private Player player;
        private int cloudcooldown = 0;
        private int searchtimer = 0;
        private int searchcooldown = 0;
        private int invhold = 0;
        private int eattimer = 0;
        private RoomCamera camera;
        private RoofTopView.DustpuffSpawner.DustPuff currentDustPuff;
        private ScavengerAbstractAI.ScavengerSquad squad;
        private AbstractCreature[] squadMembers = new AbstractCreature[100];
        private SlugBaseSaveData data;
        public static readonly PlayerKeybind Ability = PlayerKeybind.Register("bvipri.carlcat", "CarlCat", "ability", KeyCode.LeftControl, KeyCode.JoystickButton3);
        public static bool IsPostInit = false;
        // Add hooks
        public void OnEnable()
        {
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            // Put your custom hooks here!
            On.Player.Jump += Player_Jump;
            On.Player.Update += Player_Update;
            On.Player.InitiateGraphicsModule += Player_InitiateGraphicsModule;
            On.Player.Grabability += Player_Grabability;
            On.Player.ReleaseGrasp += Player_ReleaseGrasp;
            On.Player.NewRoom += Player_NewRoom;
            On.ScavengerAI.RecognizeCreatureAcceptingGift += ScavengerAI_RecognizeCreatureAcceptingGift;
            On.RoofTopView.DustpuffSpawner.DustPuff.ApplyPalette += DustPuff_ApplyPalette;
            On.RainWorld.PostModsInit += RainWorld_PostModsInit;
            On.ScavengerAI.LikeOfPlayer += ScavengerAI_LikeOfPlayer;
            On.ScavengerAI.PlayerRelationship += ScavengerAI_PlayerRelationship;
            On.Player.ObjectCountsAsFood += Player_ObjectCountsAsFood;
            On.SlugcatStats.NourishmentOfObjectEaten += SlugcatStats_NourishmentOfObjectEaten;
            On.Player.CanBeSwallowed += Player_CanBeSwallowed;
            On.ShelterDoor.Close += ShelterDoor_Close;
        }

        private void ShelterDoor_Close(On.ShelterDoor.orig_Close orig, ShelterDoor self)
        {
            orig(self);
            if (self.Broken) return;
            /*print("iterating data");
            for (int i = 0; i < squad.members.Count; i++)
            {
                if (squad.members[i] != null)
                {
                    squadMembers[i] = squad.members[i];
                }
            }
            print("setting data");
            data.Set<AbstractCreature[]>("Scavengers", squadMembers);*/
        }

        private bool Player_CanBeSwallowed(On.Player.orig_CanBeSwallowed orig, Player self, PhysicalObject testObj)
        {
            if (self.slugcatStats.name.ToString() == "carlcat")
            {
                return false;   
            }
            return orig(self, testObj);
        }
        private int SlugcatStats_NourishmentOfObjectEaten(On.SlugcatStats.orig_NourishmentOfObjectEaten orig, SlugcatStats.Name slugcatIndex, IPlayerEdible eatenobject)
        {
            if (slugcatIndex.ToString() == "carlcat")
            {
                if (eatenobject is DangleFruit || eatenobject is SlimeMold || eatenobject is EggBugEgg)
                {
                    return 2;
                }
                else if (eatenobject is BubbleGrass || eatenobject is NeedleEgg)
                {
                    return 8;
                }
                else if (eatenobject is PuffBall || eatenobject is Mushroom || eatenobject is GlowWeed || eatenobject is DandelionPeach || eatenobject is FlyLure)
                {
                    return 4;
                }
                else
                {
                    return 0;
                }
            }
            return orig(slugcatIndex, eatenobject);
        }
        private bool Player_ObjectCountsAsFood(On.Player.orig_ObjectCountsAsFood orig, Player self, PhysicalObject obj)
        {
            if (self.slugcatStats.name.ToString() == "carlcat")
            {
                if (obj is DangleFruit ||
                    obj is BubbleGrass ||
                    obj is PuffBall ||
                    obj is Mushroom ||
                    obj is SlimeMold ||
                    obj is GlowWeed ||
                    obj is DandelionPeach ||
                    obj is FlyLure ||
                    obj is NeedleEgg ||
                    obj is EggBugEgg)
                {
                    return true;
                }
            }
            return orig(self, obj);
        }
        private CreatureTemplate.Relationship ScavengerAI_PlayerRelationship(On.ScavengerAI.orig_PlayerRelationship orig, ScavengerAI self, RelationshipTracker.DynamicRelationship dRelation)
        {
            if (player.slugcatStats.name.ToString() == "carlcat")
            {
                return new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Pack, 1f);
            }
            return orig(self, dRelation);
        }
        private float ScavengerAI_LikeOfPlayer(On.ScavengerAI.orig_LikeOfPlayer orig, ScavengerAI self, RelationshipTracker.DynamicRelationship dRelation)
        {
            if (player.slugcatStats.name.ToString() == "carlcat")
            {
                return 1f;
            }
            return orig(self, dRelation);
        }
        private void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
        {
            orig.Invoke(self);
            try
            {
                bool isPostInit = Plugin.IsPostInit;
                if (!isPostInit)
                {
                    Plugin.IsPostInit = true;
                    bool flag = Enumerable.Any<ModManager.Mod>(ModManager.ActiveMods, (ModManager.Mod mod) => mod.id == "dressmyslugcat");
                    if (flag)
                    {
                        this.SetupDMSSprites();
                    }
                    Debug.Log("Plugin dressmyslugcat.carlcat is loaded!");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
        public void SetupDMSSprites()
        {
            string spriteSheetID = "carlsprite";
            for (int i = 0; i < 4; i++)
            {
                SpriteDefinitions.AddSlugcatDefault(new Customization
                {
                    Slugcat = "carlcat",
                    PlayerNumber = i,
                    CustomSprites = new List<CustomSprite>
                    {
                        new CustomSprite
                        {
                            Sprite = "HEAD",
                            SpriteSheetID = spriteSheetID
                        },
                        new CustomSprite
                        {
                            Sprite = "BODY",
                            SpriteSheetID = spriteSheetID
                        },
                        new CustomSprite
                        {
                            Sprite = "ARMS",
                            SpriteSheetID = spriteSheetID
                        },
                        new CustomSprite
                        {
                            Sprite = "HIPS",
                            SpriteSheetID = spriteSheetID
                        },
                        new CustomSprite
                        {
                            Sprite = "TAIL",
                            SpriteSheetID = spriteSheetID
                        },
                        new CustomSprite
                        {
                            Sprite = "FACE",
                            SpriteSheetID = spriteSheetID,
                            ColorHex = "#ffffff"
                        },
                        new CustomSprite
                        {
                            Sprite = "LEGS",
                            SpriteSheetID = spriteSheetID
                        }
                    }
                });
            }
        }
        private void Player_NewRoom(On.Player.orig_NewRoom orig, Player self, Room newRoom)
        {
            orig(self, newRoom);
            if (squad.members.Count > 0)
            {
                printSquad(squad);
                if (newRoom.world.region.name.ToString() == squad.members[0].Room.world.region.name.ToString())
                {
                    squad.CommonMovement(self.room.abstractRoom.index, null, false);
                } else
                {
                    squad.Dissolve();
                }
            }
        }
        private void ScavengerAI_RecognizeCreatureAcceptingGift(On.ScavengerAI.orig_RecognizeCreatureAcceptingGift orig, ScavengerAI self, Tracker.CreatureRepresentation subRep, Tracker.CreatureRepresentation objRep, bool objIsMe, PhysicalObject item)
        {
            orig(self, subRep, objRep, objIsMe, item);
            if (objIsMe == true && item.abstractPhysicalObject.type == AbstractPhysicalObject.AbstractObjectType.DataPearl)
            {
                if (squad.members.Count == 0)
                {
                    (self.creature.abstractAI as ScavengerAbstractAI).TryAssembleSquad();
                    squad = (self.creature.abstractAI as ScavengerAbstractAI).squad;
                    squad.missionType = ScavengerAbstractAI.ScavengerSquad.MissionID.ProtectCreature;
                    squad.targetCreature = player.abstractCreature;
                    player.room.world.scavengersWorldAI.playerAssignedSquads.Add(squad);
                    player.room.world.scavengersWorldAI.ResetSquadCooldown(1f);
                }
                else
                {
                    squad.AddMember(self.creature);
                }
            }
        }

        private void Player_ReleaseGrasp(On.Player.orig_ReleaseGrasp orig, Player self, int grasp)
        {
            if (self.slugcatStats.name.ToString() == "carlcat")
            {
                if (self.grasps[grasp] != null)
                {
                    self.grasps[grasp].grabbed.g = 1;
                }
            }
            orig(self, grasp);
        }
        private Player.ObjectGrabability Player_Grabability(On.Player.orig_Grabability orig, Player self, PhysicalObject obj)
        {
            if (self.slugcatStats.name.ToString() == "carlcat")
            {
                var isObjectInInventory = (self.grasps[2] != null && self.grasps[2].grabbed == obj) ? true : (self.grasps[3] != null && self.grasps[3].grabbed == obj) ? true : (self.grasps[4] != null && self.grasps[4].grabbed == obj) ? true : false;
                if (isObjectInInventory == true)
                {
                    return Player.ObjectGrabability.CantGrab;
                }
            }
            return orig(self, obj);
        }
        private void Player_InitiateGraphicsModule(On.Player.orig_InitiateGraphicsModule orig, Player self)
        {
            orig(self);
            player = self;
            if (self.slugcatStats.name.ToString() == "carlcat")
            {
                self.grasps = new Player.Grasp[5];
                data = SaveDataExtension.GetSlugBaseData(self.room.game.GetStorySession.saveState.miscWorldSaveData);
                squad = new ScavengerAbstractAI.ScavengerSquad(player.abstractCreature);
                squad.members.Clear();
                if (data.TryGet<AbstractCreature[]>("Scavengers", out squadMembers) == true)
                {
                    print("found data");
                    for (int i=0; i<squadMembers.Length; i++)
                    {
                        if (squadMembers[i] != null)
                        {
                            print(squadMembers[i].ID);
                            squad.AddMember(squadMembers[i]);
                        }
                    }
                }
            }
        }

        private void printSquad(ScavengerAbstractAI.ScavengerSquad squad)
        {
            String members = "";
            for (int i = 0; i < squad.members.Count; i++)
            {
                members = members + squad.members[i].creatureTemplate.type.ToString() + ",";
            }
            print("Squad with members: " + members + " with leader scav ID " + squad.leader.ID + " with mission " + squad.missionType.ToString() + " that has a mission? " + squad.HasAMission.ToString() + " in the room " + squad.MissionRoom);
        }
        private void DustPuff_ApplyPalette(On.RoofTopView.DustpuffSpawner.DustPuff.orig_ApplyPalette orig, RoofTopView.DustpuffSpawner.DustPuff self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            if (self.Equals(currentDustPuff))
            {
                sLeaser.sprites[0].color = new Color(palette.blackColor.r + 0.1f, palette.blackColor.g + 0.1f, palette.blackColor.b + 0.1f);
            }
        }
        // Load any resources, such as sprites or sounds
        private void LoadResources(RainWorld rainWorld)
        {
        }
        private void UpdateGrabs(Player self)
        {
            for (int i = 0; i < self.grasps.Length; i++)
            {
                if (self.grasps[i] != null)
                {
                    if (i > 1)
                    {
                        var obj = self.grasps[i].grabbed;
                        obj.firstChunk.vel = new Vector2(0f, 0f);
                        obj.firstChunk.pos = self.bodyChunks[1].pos + new Vector2(-6f * (i - 3), 0f);
                    }
                }
            }
        }
        private void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            // Check what slugcat
            if (self.slugcatStats.name.ToString() == "skrungle") // is skrungle
            {
                if (self.input[0].AnyDirectionalInput) // Activate velocity boosts
                {
                    // make sure that vile creature is behaving
                    if (
                        self.bodyMode != Player.BodyModeIndex.Crawl &&
                        self.bodyMode != Player.BodyModeIndex.Dead &&
                        self.bodyMode != Player.BodyModeIndex.Stand &&
                        self.bodyMode != Player.BodyModeIndex.Stunned &&
                        self.bodyMode != Player.BodyModeIndex.ClimbingOnBeam &&
                        self.bodyMode != Player.BodyModeIndex.ClimbIntoShortCut &&
                        self.bodyMode != Player.BodyModeIndex.CorridorClimb &&
                        self.bodyMode != Player.BodyModeIndex.WallClimb
                        )
                    {
                        // variables
                        float velMultX = 1.05f;
                        float velMultY = 1.05f;
                        var room = self.room;
                        var pos = self.mainBodyChunk.pos;
                        var vel = self.mainBodyChunk.vel;

                        if (cloudcooldown <= 0)
                        {
                            cloudcooldown = 15;

                            // visual
                            Smoke.FireSmoke smoke = new FireSmoke(room);
                            room.AddObject(smoke);
                            room.PlaySound(SoundID.Cyan_Lizard_Small_Jump, pos, 1f, 0.5f + UnityEngine.Random.value * 0.5f);
                            for (int i = 1; i < 10; i++)
                            {
                                smoke.EmitSmoke(pos, vel * UnityEngine.Random.value * 1.5f, new Color(3f, 4f, 0.5f), 20);
                            }
                        }
                        else
                        {
                            cloudcooldown -= 1;
                        }

                        // velocity
                        self.mainBodyChunk.vel = new Vector2(vel.x * velMultX, vel.y * velMultY);
                    }
                }
            }
            else if (self.slugcatStats.name.ToString() == "carlcat") // is carlcat
            {
                UpdateGrabs(self);

                // friends!
                if (squad.members.Count > 0)
                {
                    for (int i = 0; i < squad.members.Count; i++)
                    {
                        if (squad.members[i].Room == self.room.abstractRoom)
                        {
                            var pos1 = self.mainBodyChunk.pos;
                            var pos2 = squad.members[i].realizedCreature.mainBodyChunk.pos;
                            if ((pos1 - pos2).magnitude > 30)
                            {
                                squad.members[i].abstractAI.SetDestination(self.coord);
                            }
                        }
                    }
                }

                // separate code for eating stuff because i cant put IPlayerEdible on existing objects without exploding :)
                if (self.input[0].pckp == true)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var grab = self.grasps[i];
                        if (grab != null && (grab.grabbed is NeedleEgg || grab.grabbed is BubbleGrass || grab.grabbed is FlyLure || grab.grabbed is PuffBall))
                        {
                            eattimer += 1;
                            if ((eattimer > 30 && eattimer < 34) || (eattimer > 40 && eattimer < 44) || (eattimer > 50 && eattimer < 54))
                            {
                                (self.graphicsModule as PlayerGraphics).hands[i].absoluteHuntPos = self.bodyChunks[0].pos;
                                (self.graphicsModule as PlayerGraphics).hands[i].reachingForObject = true;
                            }
                            if (eattimer > 60)
                            {
                                eattimer = 0;
                                var obj = grab.grabbed;
                                int points = obj is NeedleEgg ? 2 : obj is BubbleGrass ? 2 : obj is FlyLure ? 1 : obj is PuffBall ? 1 : 0;
                                self.ReleaseGrasp(i);
                                obj.room.RemoveObject(obj);
                                obj.Destroy();
                                self.AddFood(points);
                            }
                        }
                    }
                }

                // search ability
                if (self.input[0].jmp == true && self.input[0].y == -1 && self.bodyMode == Player.BodyModeIndex.Crawl && searchcooldown <= 0)
                {
                    searchtimer += 1;

                    // variables
                    var room = self.room;
                    var pos = self.mainBodyChunk.pos;
                    var vel = self.mainBodyChunk.vel;
                    var rdm = UnityEngine.Random.value;

                    if (rdm < 0.3)
                    {
                        // animation
                        (self.graphicsModule as PlayerGraphics).hands[0].absoluteHuntPos = new Vector2(pos.x + 20 * UnityEngine.Random.value * self.ThrowDirection, pos.y - 10 - 10 * UnityEngine.Random.value);
                        (self.graphicsModule as PlayerGraphics).hands[0].reachingForObject = true;
                        (self.graphicsModule as PlayerGraphics).hands[1].absoluteHuntPos = new Vector2(pos.x + 20 * UnityEngine.Random.value * self.ThrowDirection, pos.y - 10 - 10 * UnityEngine.Random.value);
                        (self.graphicsModule as PlayerGraphics).hands[1].reachingForObject = true;

                        // visual
                        RoofTopView.DustpuffSpawner.DustPuff dustPuff = new RoofTopView.DustpuffSpawner.DustPuff(new Vector2(pos.x + 10 * self.ThrowDirection, pos.y - 5), 0.7f);
                        currentDustPuff = dustPuff;
                        room.AddObject(dustPuff);

                        // audio
                        if (rdm < 0.1)
                        {
                            room.PlaySound(SoundID.Rock_Hit_Wall, pos, 0.8f, 0.5f + UnityEngine.Random.value);
                        }
                        else
                        {
                            room.PlaySound(SoundID.Rock_Hit_Wall, pos, 0.3f, 0.5f + UnityEngine.Random.value);
                        }
                    }

                    if (searchtimer > 200) // searched long enough
                    {
                        searchcooldown = 200;
                        searchtimer = 0;
                        bool spear = false;
                        for (int i = 0; i < self.grasps.Length; i++) // check if theyre holding a spear
                        {
                            if (self.grasps[i] != null)
                            {
                                if (self.grasps[i].grabbed.abstractPhysicalObject.type == AbstractPhysicalObject.AbstractObjectType.Spear)
                                {
                                    spear = true; break;
                                }
                            }
                        }
                        if (self.FreeHand() != -1 && self.FreeHand() < 2) // if they have a hand free and its not the inventory
                        {
                            room.PlaySound(SoundID.Rock_Hit_Wall, pos, 2f, 1.1f);
                            if (spear) // only run chances for rock and scav bomb
                            {
                                float random = UnityEngine.Random.value;
                                if (random > 0.2) // add rock
                                {
                                    AbstractPhysicalObject obj = new AbstractPhysicalObject(self.room.world, AbstractPhysicalObject.AbstractObjectType.Rock, null, self.coord, self.room.world.game.GetNewID());
                                    self.room.abstractRoom.AddEntity(obj);
                                    obj.RealizeInRoom();
                                    self.SlugcatGrab(obj.realizedObject, self.FreeHand());
                                    room.PlaySound(SoundID.Slugcat_Pick_Up_Rock, pos, 2f, 1f);
                                }
                                else // add scav bomb
                                {
                                    AbstractPhysicalObject obj = new AbstractPhysicalObject(self.room.world, AbstractPhysicalObject.AbstractObjectType.ScavengerBomb, null, self.coord, self.room.world.game.GetNewID());
                                    self.room.abstractRoom.AddEntity(obj);
                                    obj.RealizeInRoom();
                                    self.SlugcatGrab(obj.realizedObject, self.FreeHand());
                                    room.PlaySound(SoundID.Slugcat_Pick_Up_Bomb, pos, 2f, 1f);
                                }
                            }
                            else // all chances
                            {
                                float random = UnityEngine.Random.value;
                                if (random < 0.5) // add rock
                                {
                                    AbstractPhysicalObject obj = new AbstractPhysicalObject(self.room.world, AbstractPhysicalObject.AbstractObjectType.Rock, null, self.coord, self.room.world.game.GetNewID());
                                    self.room.abstractRoom.AddEntity(obj);
                                    obj.RealizeInRoom();
                                    self.SlugcatGrab(obj.realizedObject, self.FreeHand());
                                    room.PlaySound(SoundID.Slugcat_Pick_Up_Rock, pos, 2f, 1f);
                                }
                                else if (random < 0.8) // add spear
                                {
                                    AbstractSpear obj = new AbstractSpear(self.room.world, null, self.coord, self.room.world.game.GetNewID(), false);
                                    self.room.abstractRoom.AddEntity(obj);
                                    obj.RealizeInRoom();
                                    self.SlugcatGrab(obj.realizedObject, self.FreeHand());
                                    room.PlaySound(SoundID.Slugcat_Pick_Up_Spear, pos, 2f, 1f);
                                }
                                else if (random < 0.9) // add scavenger bomb 
                                {
                                    AbstractPhysicalObject obj = new AbstractPhysicalObject(self.room.world, AbstractPhysicalObject.AbstractObjectType.ScavengerBomb, null, self.coord, self.room.world.game.GetNewID());
                                    self.room.abstractRoom.AddEntity(obj);
                                    obj.RealizeInRoom();
                                    self.SlugcatGrab(obj.realizedObject, self.FreeHand());
                                    room.PlaySound(SoundID.Slugcat_Pick_Up_Bomb, pos, 2f, 1f);
                                }
                                else // add explosive spear
                                {
                                    self.room.world.game.GetNewID();
                                    AbstractSpear obj = new AbstractSpear(self.room.world, null, self.coord, self.room.world.game.GetNewID(), true);
                                    self.room.abstractRoom.AddEntity(obj);
                                    obj.RealizeInRoom();
                                    self.SlugcatGrab(obj.realizedObject, self.FreeHand());
                                    room.PlaySound(SoundID.Slugcat_Pick_Up_Spear, pos, 2f, 1f);
                                }
                            }
                        }
                    }
                }
                else
                {
                    searchcooldown -= 1;
                    searchtimer = 0;
                }
                // inventory ability
                if (Ability.CheckRawPressed(self.playerState.playerNumber))
                {
                    invhold += 1;
                    if (self.input[0].y == 1)
                    {
                        var usedslot = self.grasps[4] != null ? 4 : self.grasps[3] != null ? 3 : self.grasps[2] != null ? 2 : -1;
                        var freehand = self.grasps[0] == null ? 0 : self.grasps[1] == null ? 1 : -1;
                        var pos = self.bodyChunks[1].pos;
                        if (freehand != -1 && usedslot != -1)
                        {
                            (self.graphicsModule as PlayerGraphics).hands[freehand].absoluteHuntPos = new Vector2(pos.x - 6f * (usedslot - 3), pos.y);
                            (self.graphicsModule as PlayerGraphics).hands[freehand].reachingForObject = true;
                            if (invhold > 10)
                            {
                                invhold = 0;
                                var obj = self.grasps[usedslot].grabbed;
                                obj.g = 1;
                                self.ReleaseObject(usedslot, false);
                                self.Grab(obj, freehand, 0, Creature.Grasp.Shareability.CanNotShare, 0, false, false);
                            }
                        }
                    }
                    else
                    {
                        var freeslot = self.grasps[2] == null ? 2 : self.grasps[3] == null ? 3 : self.grasps[4] == null ? 4 : -1;
                        var usedhand = self.grasps[0] != null ? 0 : self.grasps[1] != null ? 1 : -1;
                        var pos = self.bodyChunks[1].pos;
                        if (freeslot != -1 && usedhand != -1)
                        {
                            (self.graphicsModule as PlayerGraphics).hands[usedhand].absoluteHuntPos = new Vector2(pos.x - 6f * (freeslot - 3), pos.y);
                            (self.graphicsModule as PlayerGraphics).hands[usedhand].reachingForObject = true;
                            if (invhold > 10 && self.Grabability(self.grasps[usedhand].grabbed) == Player.ObjectGrabability.OneHand)
                            {
                                invhold = 0;
                                var obj = self.grasps[usedhand].grabbed;
                                obj.g = 0;
                                self.ReleaseObject(usedhand, false);
                                self.Grab(obj, freeslot, 0, Creature.Grasp.Shareability.CanNotShare, 0, false, false);
                            }
                        }
                    }
                }
                else
                {
                    invhold = 0;
                }
            }
        }
        private void Player_Jump(On.Player.orig_Jump orig, Player self)
        {
            orig(self);

            // Check what slugcat
            if (self.slugcatStats.name.ToString() == "skrungle") // is skrungle 
            {
                // Activate cloud jump

                // variables
                var room = self.room;
                var pos = self.mainBodyChunk.pos;
                var vel = self.mainBodyChunk.vel;

                // visual
                Smoke.FireSmoke smoke = new Smoke.FireSmoke(room);
                room.AddObject(smoke);
                room.PlaySound(SoundID.Cyan_Lizard_Medium_Jump, pos, 0.8f, 0.5f + UnityEngine.Random.value * 0.5f);
                for (int i = 0; i < 10; i++)
                {
                    smoke.EmitSmoke(pos, vel * UnityEngine.Random.value * 0.5f, new Color(4f, 5f, 1f), 20);
                }

                // velocity
                self.mainBodyChunk.vel = new Vector2(vel.x, vel.y + 3);
            }
        }
    }
}