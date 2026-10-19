using System;
using System.IO;

namespace SilkJournal;

public sealed partial class JournalState
{
 public static JournalState Starter()
 {
  var project=new Quest{Title="Launch a portfolio",Notes="Make a home for your best work. One small step at a time.",Children=new()
  {
   new Quest{Title="Collect references",Done=true},new Quest{Title="Choose projects",Done=true},
   new Quest{Title="Build the home page",Notes="A first impression and a selection of your best work.",Children=new()
   {
    new Quest{Title="Headline",Done=true},new Quest{Title="Project cards"},new Quest{Title="Mobile version"}
   }},new Quest{Title="Publish the website"}
  }};
  using var art=typeof(JournalState).Assembly.GetManifestResourceStream("SilkJournal.Assets.starter-pepsi.png")!;
  using var image=new MemoryStream();art.CopyTo(image);
  var state=new JournalState{Language="en",FocusSessionMinutes=25,SelectedId=project.Children[2].Id,Quests=new()
  {
   project,new Quest{Title="Clear the inbox",Notes="Reply to what matters. The rest can wait."},
   new Quest{Title="Take a walk · 20 minutes",Notes="Rest is part of the journey."}
  },ShopItems=new()
  {
   new ShopItem{Name="Pepsi Bottle",Description="A revitalizing elixir for the creator of this app.",Price=15,Image=Convert.ToBase64String(image.ToArray())}
  }};
  state.InitializeRewards();return state;
 }
}
