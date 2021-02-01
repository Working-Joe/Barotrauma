﻿using Microsoft.Xna.Framework;
using System;

namespace Barotrauma.Networking
{
    partial class ChatMessage
    {
        public virtual void ClientWrite(IWriteMessage msg)
        {
            msg.Write((byte)ClientNetObject.CHAT_MESSAGE);
            msg.Write(NetStateID);
            msg.Write((byte)Type);
            msg.Write(Text);
        }

        public static void ClientRead(IReadMessage msg)
        {
            UInt16 ID = msg.ReadUInt16();
            ChatMessageType type = (ChatMessageType)msg.ReadByte();
            PlayerConnectionChangeType changeType = PlayerConnectionChangeType.None;
            string txt = "";
            string styleSetting = string.Empty;

            if (type != ChatMessageType.Order)
            {
                changeType = (PlayerConnectionChangeType)msg.ReadByte();
                txt = msg.ReadString();
            }

            string senderName = msg.ReadString();
            Character senderCharacter = null;
            bool hasSenderCharacter = msg.ReadBoolean();
            if (hasSenderCharacter)
            {
                senderCharacter = Entity.FindEntityByID(msg.ReadUInt16()) as Character;
                if (senderCharacter != null)
                {
                    senderName = senderCharacter.Name;
                }
            }

            switch (type)
            {
                case ChatMessageType.Default:
                    break;
                case ChatMessageType.Order:
                    int orderIndex = msg.ReadByte();
                    UInt16 targetCharacterID = msg.ReadUInt16();
                    Character targetCharacter = Entity.FindEntityByID(targetCharacterID) as Character;
                    Entity targetEntity = Entity.FindEntityByID(msg.ReadUInt16());
                    int optionIndex = msg.ReadByte();
                    OrderTarget orderTargetPosition = null;
                    Order.OrderTargetType orderTargetType = (Order.OrderTargetType)msg.ReadByte();
                    int wallSectionIndex = 0;
                    if (msg.ReadBoolean())
                    {
                        var x = msg.ReadSingle();
                        var y = msg.ReadSingle();
                        var hull = Entity.FindEntityByID(msg.ReadUInt16()) as Hull;
                        orderTargetPosition = new OrderTarget(new Vector2(x, y), hull, creatingFromExistingData: true);
                    }
                    else if(orderTargetType == Order.OrderTargetType.WallSection)
                    {
                        wallSectionIndex = msg.ReadByte();
                    }

                    Order orderPrefab;
                    if (orderIndex < 0 || orderIndex >= Order.PrefabList.Count)
                    {
                        DebugConsole.ThrowError("Invalid order message - order index out of bounds.");
                        if (NetIdUtils.IdMoreRecent(ID, LastID)) { LastID = ID; }
                        return;
                    }
                    else
                    {
                        orderPrefab = Order.PrefabList[orderIndex];
                    }
                    string orderOption = "";
                    if (optionIndex >= 0 && optionIndex < orderPrefab.Options.Length)
                    {
                        orderOption = orderPrefab.Options[optionIndex];
                    }
                    txt = orderPrefab.GetChatMessage(targetCharacter?.Name, senderCharacter?.CurrentHull?.DisplayName, givingOrderToSelf: targetCharacter == senderCharacter, orderOption: orderOption);

                    if (GameMain.Client.GameStarted && Screen.Selected == GameMain.GameScreen)
                    {
                        Order order = null;
                        switch (orderTargetType)
                        {
                            case Order.OrderTargetType.Entity:
                                order = new Order(orderPrefab, targetEntity, orderPrefab.GetTargetItemComponent(targetEntity as Item), orderGiver: senderCharacter);
                                break;
                            case Order.OrderTargetType.Position:
                                order = new Order(orderPrefab, orderTargetPosition, orderGiver: senderCharacter);
                                break;
                            case Order.OrderTargetType.WallSection:
                                order = new Order(orderPrefab, targetEntity as Structure, wallSectionIndex, orderGiver: senderCharacter);
                                break;
                        }

                        if (order != null)
                        {
                            if (order.TargetAllCharacters)
                            {
                                var fadeOutTime = !orderPrefab.IsIgnoreOrder ? (float?)orderPrefab.FadeOutTime : null;
                                GameMain.GameSession?.CrewManager?.AddOrder(order, fadeOutTime);
                            }
                            else if (targetCharacter != null)
                            {
                                targetCharacter.SetOrder(order, orderOption, senderCharacter);
                            }
                        }
                    }

                    if (NetIdUtils.IdMoreRecent(ID, LastID))
                    {
                        GameMain.Client.AddChatMessage(
                            new OrderChatMessage(orderPrefab, orderOption, txt, orderTargetPosition ?? targetEntity as ISpatialEntity, targetCharacter, senderCharacter));
                        LastID = ID;
                    }
                    return;
                case ChatMessageType.ServerMessageBox:
                    txt = TextManager.GetServerMessage(txt);
                    break;
                case ChatMessageType.ServerMessageBoxInGame:
                    styleSetting = msg.ReadString();
                    txt = TextManager.GetServerMessage(txt);
                    break;
            }

            if (NetIdUtils.IdMoreRecent(ID, LastID))
            {
                switch (type)
                {
                    case ChatMessageType.MessageBox:
                    case ChatMessageType.ServerMessageBox:
                        //only show the message box if the text differs from the text in the currently visible box
                        if ((GUIMessageBox.VisibleBox as GUIMessageBox)?.Text?.Text != txt)
                        {
                            new GUIMessageBox("", txt);
                        }
                        break;
                    case ChatMessageType.ServerMessageBoxInGame:
                        new GUIMessageBox("", txt, new string[0], type: GUIMessageBox.Type.InGame, iconStyle: styleSetting);
                        break;
                    case ChatMessageType.Console:
                        DebugConsole.NewMessage(txt, MessageColor[(int)ChatMessageType.Console]);
                        break;
                    case ChatMessageType.ServerLog:
                        if (!Enum.TryParse(senderName, out ServerLog.MessageType messageType))
                        {
                            return;
                        }
                        GameMain.Client.ServerSettings.ServerLog?.WriteLine(txt, messageType);
                        break;
                    default:
                        GameMain.Client.AddChatMessage(txt, type, senderName, senderCharacter, changeType);
                        break;
                }
                LastID = ID;
            }
        }
    }
}
