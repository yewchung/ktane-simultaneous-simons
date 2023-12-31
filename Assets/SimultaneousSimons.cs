using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class SimultaneousSimons : MonoBehaviour {

   public KMBombInfo Bomb;
   public KMAudio Audio;
	 public KMColorblindMode Colorblind;
	 public KMSelectable[] btnSelectables;
	 public Light[] Lights;
	 public MeshRenderer[] btnRenderers;
	 public GameObject[] colorblindTexts;
	 private static readonly string[] sounds = { "Blue Bleep", "Yellow Bleep", "Red Bleep", "Green Bleep"};
	 private static readonly string[] groupnames = {"Horizontal", "Vertical", "Corners", "Right-T", "Left-T"};
	 private static readonly string[] colornames = {"Blue", "Yellow", "Red", "Green"};

   static int ModuleIdCounter = 1;
   int ModuleId;
   private bool ModuleSolved;
	 private static readonly Color32[] materialColors = { new Color32(0, 0, 155, 255), new Color32(155, 155, 0, 255), new Color32(155, 0, 0, 255), new Color32(0, 155, 0, 255) };
	 private static readonly Color32[] flashyColors = { new Color32(155, 155, 255, 255), new Color32(255, 255, 155, 255), new Color32(255, 155, 155, 255), new Color32(155, 255, 155, 255) };
	 private static readonly int[] buttonColors = {0,1,2,3,2,3,0,1,1,0,3,2,3,2,1,0};
	 private int groups = 0;
	 private static readonly int[][][] groupings = new int[][][]{
		 new int[][]{
			 new int[]{0,1,2,3}, new int[]{4,5,6,7}, new int[]{8,9,10,11}, new int[]{12,13,14,15}},
 		 new int[][]{
			 new int[]{0,4,8,12}, new int[]{1,5,9,13}, new int[]{2,6,10,14}, new int[]{3,7,11,15}},
		 new int[][]{
			 new int[]{0,1,4,5}, new int[]{8,9,12,13}, new int[]{2,3,6,7}, new int[]{10,11,14,15}},
		 new int[][]{
			 new int[]{0,1,2,5}, new int[]{4,8,9,12}, new int[]{3,6,7,11}, new int[]{10,13,14,15}},
		 new int[][]{
			 new int[]{0,4,5,8}, new int[]{1,2,3,6}, new int[]{9,12,13,14}, new int[]{7,10,11,15}}
		 };
	 private static readonly int[][][] colorChanges = new int[][][]{
		 new int[][]{
			 new int[]{2,3,0,1}, new int[]{3,2,1,0}, new int[]{2,0,3,1}},
		 new int[][]{
			 new int[]{1,2,0,3}, new int[]{0,3,2,1}, new int[]{3,2,1,0}}
	 };
	 private int[,] sequences = new int[4,4];
	 private int[] flashnum = {0,0,0,0};
	 private int stagenum = 0;
	 private int[] substagenum = {0,0,0,0};
	 private int[] validButtons = {-1,-1,-1,-1};
	 private bool buttonPressed = false, playSounds = false;
	 private int serial = 0;
   private int savedStrikes = 0;

   void Awake () {
      ModuleId = ModuleIdCounter++;
      /*
      foreach (KMSelectable object in keypad) {
          object.OnInteract += delegate () { keypadPress(object); return false; };
      }
      */

      //button.OnInteract += delegate () { buttonPress(); return false; };

   }

   void Start () {
		 float scalar = transform.lossyScale.x;
		 for (var i = 0; i < Lights.Length; i++)
		 {
				 Lights[i].range *= scalar;
				 Lights[i].intensity = 5;
				 Lights[i].enabled = false;
		 }
		 for (int i = 0; i < 16; i++) {
			 int j = i;
			 btnRenderers[j].material.color = materialColors[buttonColors[j]];
			 btnSelectables[j].OnInteract += delegate ()
			 {
					 StopAllCoroutines();
					 if (!ModuleSolved){
							 ButtonPress(j);
							 for (int k = 0; k < 16; k++) {
								 btnRenderers[k].material.color = materialColors[buttonColors[k]];
								 Lights[k].enabled = false;
							 }
					 }
					 btnSelectables[j].AddInteractionPunch(2);
					 StartCoroutine(ButtonAnim(j));
					 return false;
			 };
		 }

		 groups = Rnd.Range(0, 5);

		 int[] temp = {0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15};
		 var choices = new List<int>(temp);
		 int done = 0;
		 int[] selected = {-1,-1,-1,-1};
		 var invalidated = new List<int>();
		 while (done < 4){
			 int choice = choices[Rnd.Range(0,choices.Count)];
			 selected[done] = choice;

			 done++;
			 if (done >= 4) {
				 bool valid = CheckSelected(selected);
				 if (!valid) {
					 done--;
					 choices.Remove(choice);
					 if (choices.Count <= 0) {
						 done--;
						 choices.AddRange(groupings[groups][findGroup(choice)]);
						 int t = selected[done];
						 invalidated.Add(t);
						 choices.AddRange(groupings[groups][findGroup(t)]);
						 removeAll(choices, invalidated);
					 }
				 }
			 } else {
				 removeAll(choices, new List<int>(groupings[groups][findGroup(choice)]));
			 }
		 }

		 for (int i = 0; i < 4; i++) {
			 sequences[findGroup(selected[i]),0] = selected[i];
		 }

		 DebugMessage("The selected Simon grouping is " + (groups + 1) + ", aka " + groupnames[groups] + ".");
		 DebugMessage("The initial flashing buttons are: " + colornames[buttonColors[sequences[0,0]]] + " for Simon 1, " +
		 							colornames[buttonColors[sequences[1,0]]] + " for Simon 2, " + colornames[buttonColors[sequences[2,0]]] +
									" for Simon 3, and " + colornames[buttonColors[sequences[3,0]]] + " for Simon 4.");

		 setValidButtons();

     PrintCorrectButtons();
     savedStrikes = Bomb.GetStrikes();

		 StartCoroutine(FlashingLights(0));
		 StartCoroutine(FlashingLights(1));
		 StartCoroutine(FlashingLights(2));
		 StartCoroutine(FlashingLights(3));
   }

	 void ButtonPress(int btn) {
     if (savedStrikes != Bomb.GetStrikes()) {
       DebugMessage("Number of Strikes changed since solution was last printed. New solution for " + Bomb.GetStrikes() + " strikes:");
       PrintCorrectButtons();
       savedStrikes = Bomb.GetStrikes();
     }
		 buttonPressed = true;
		playSounds = true;
		 DebugMessage("Pressed the " + colornames[buttonColors[btn]] + " button for Simon " + (findGroup(btn) + 1) + ".");
     setValidButtons();
		 if (!validButtons.Contains(btn)) {
			 GetComponent<KMBombModule>().HandleStrike();
			 for(int i = 0; i < 4; i++) {
				 substagenum[i] = 0;
			 }
			 DebugMessage("Strike! New solution for " + Bomb.GetStrikes() + " strikes:");
			 setValidButtons();
			 PrintCorrectButtons();
       savedStrikes = Bomb.GetStrikes();

			 StartCoroutine(FlashingLights(0));
			 StartCoroutine(FlashingLights(1));
			 StartCoroutine(FlashingLights(2));
			 StartCoroutine(FlashingLights(3));
		 } else {
			 int t = Array.IndexOf(validButtons, btn);
			 substagenum[t]++;
			 if (substagenum.Where(i => i > stagenum).ToArray().Length >= 4) {
				 nextStage();
			 } else {
				 setValidButtons();
			 }
		 }
	 }

   IEnumerator FlashingLights(int num) {
		 	flashnum[num] = 0;
			float delay = 1f;
			if (stagenum > 0) {
				delay = Rnd.value * (4.6f + (0.6f * (stagenum + 1)));
			}
	    yield return new WaitForSeconds(1.5f + delay);
			while (!ModuleSolved) {
				if (flashnum[num] <= stagenum) {
					int i = sequences[num,flashnum[num]];
		 			btnRenderers[i].material.color = flashyColors[buttonColors[i]];
					Lights[i].enabled = true;
					if (buttonPressed && playSounds) {
						Audio.PlaySoundAtTransform(sounds[buttonColors[i]], btnRenderers[i].transform);
					}
					//Lights[i].intensity = 5;

					yield return new WaitForSeconds(0.3f);

					btnRenderers[i].material.color = materialColors[buttonColors[i]];
					Lights[i].enabled = false;
					//Lights[i].intensity = 1;
					yield return new WaitForSeconds(0.3f);

					flashnum[num]++;
				} else {
          if (buttonPressed) {
            yield return new WaitForSeconds(4.6f);
          } else {
					  yield return new WaitForSeconds(2.3f);
          }
					flashnum[num] = 0;
				}
			}
	 }

	 IEnumerator ButtonAnim(int i) {
		 btnRenderers[i].material.color = flashyColors[buttonColors[i]];
		 Lights[i].enabled = true;
		 Audio.PlaySoundAtTransform(sounds[buttonColors[i]], btnRenderers[i].transform);
		 yield return new WaitForSeconds(0.3f);
		 btnRenderers[i].material.color = materialColors[buttonColors[i]];
		 Lights[i].enabled = false;
	 }

	 bool CheckSelected(int[] selected) {
		 bool trueValid = true;
		 for (int i = 0; i < 5; i++) {
			 if (i != groups) {
				 bool valid = false;
				 for (int j = 0; j < 4; j++) {
					 int contains = 0;
					 for (int k = 0; k < 4; k++) {
						 if(groupings[i][j].Contains(selected[k])){
							 contains++;
						 }
					 }
					 if (contains != 1){
						 valid = true;
					 }
				 }
				 if (!valid){
					 trueValid = false;
				 }
			 }
		 }
		 return trueValid;
	 }

	 int findGroup(int check) {
		 int currgroup = -1;
		 for (int i = 0; i < 4; i++) {
			 if (groupings[groups][i].Contains(check)){
				 currgroup = i;
				 break;
			 }
		 }
		 return currgroup;
	 }

	 void removeAll(System.Collections.Generic.List<int> list, System.Collections.Generic.IEnumerable<int> collection) {
		 for (int i = 0; i < collection.Count(); i++) {
			 list.Remove(collection.ElementAt(i));
		 }
	 }

	 void nextStage() {
		 if (stagenum >= 3) {
       DebugMessage("Module Solved!");
			 GetComponent<KMBombModule>().HandlePass();
			 ModuleSolved = true;
		 } else {
			 stagenum++;
			 for(int i = 0; i < 4; i++) {
				 substagenum[i] = 0;
			 }
			 for (int i = 0; i < 4; i++) {
				 int a = Rnd.Range(0,4);
				 sequences[i,stagenum] = groupings[groups][i][a];
				 string colorsequence = "";
				 for (int j = 0; j < stagenum; j++) {
					 colorsequence += colornames[buttonColors[sequences[i,j]]];
					 colorsequence += ", ";
				 }
				 colorsequence += colornames[buttonColors[sequences[i,stagenum]]];
				 DebugMessage("Stage " + (stagenum + 1) + ": Flash sequence for Simon " + (i + 1) + " is " + colorsequence + ".");
			 }

			 setValidButtons();
			 PrintCorrectButtons();
       savedStrikes = Bomb.GetStrikes();
			 StartCoroutine(FlashingLights(0));
			 StartCoroutine(FlashingLights(1));
			 StartCoroutine(FlashingLights(2));
			 StartCoroutine(FlashingLights(3));
		 }
	 }

	 void setValidButtons() {
     var serialNum = Bomb.GetSerialNumber().ToUpperInvariant();
     string vowels = "AEIOU";
     if (serialNum.ToArray().Intersect(vowels.ToArray()).Count() > 0) {
       serial = 0;
     } else {
       serial = 1;
     }
		 for(int i = 0; i < 4; i++) {
			 validButtons[i] = -1;
		 }
		 int strikes = (Bomb.GetStrikes() % 3);
		 for (int i = 0; i < 4; i++) {
			 if (substagenum[i] <= stagenum) {
				 int basecolor = buttonColors[sequences[i,substagenum[i]]];
				 int newcolor = colorChanges[serial][(strikes+i)%3][basecolor];
				 for (int j = 0; j < 4; j++) {
					 int checkButton = groupings[groups][i][j];
					 if (buttonColors[checkButton] == newcolor){
						 validButtons[i] = checkButton;
						 j = 4;
					 }
				 }
			 }
		 }
	 }

	 void PrintCorrectButtons() {
		 int strikes = (Bomb.GetStrikes() % 3);
		 for (int i = 0; i < 4; i++) {
			 string colorsequence = "";
			 for (int j = 0; j < stagenum; j++) {
				 colorsequence += colornames[colorChanges[serial][(strikes+i)%3][buttonColors[sequences[i,j]]]];
				 colorsequence += ", ";
			 }
			 colorsequence += colornames[colorChanges[serial][(strikes+i)%3][buttonColors[sequences[i,stagenum]]]];
			 DebugMessage("Stage " + (stagenum + 1) + ": Correct sequence for Simon " + (i + 1) + " is " + colorsequence + ".");
		 }
	 }

   void DebugMessage(string message) {
		 Debug.LogFormat("[Simultaneous Simons #{0}] {1}", ModuleId, message);
	 }

#pragma warning disable 414
   private readonly string TwitchHelpMessage = "\"!{0} press (A/B/C/D)(1/2/3/4)\" or \"!{0} press (1/2/3/4)(A/B/C/D)\" [Press buttons corresponding to the coordinate of the module, 1-4 going from top-right to bottom-left rows, A-D going from top-left to bottom-right columns. Multiple button presses can be chained on the module by appending multiples of the desired buttons.] | \"!{0} mute\" [Mutes the loud sounds coming from the module.]";
#pragma warning restore 414

   IEnumerator ProcessTwitchCommand (string Command) {
		var intCmd = Command.Trim();
		var matchPressCmd = Regex.Match(intCmd, @"^press(\s([abcd][1234]|[1234][abcd]))+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		var matchMute = Regex.Match(intCmd, @"^mute$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (matchMute.Success)
		{
			yield return null;
			playSounds = false;
		}
		else if (matchPressCmd.Success)
        {
			var matchesAllPresses = Regex.Matches(matchPressCmd.Value.ToLowerInvariant().Replace("press",""), @"[abcd][1234]|[1234][abcd]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			var allPresses = new List<KMSelectable>();
			foreach (Match curMatch in matchesAllPresses)
            {
				Debug.LogFormat("\"{0}\"", curMatch.Value);
				var curStr = curMatch.Value;
				var _1stChrStr = curStr[0];
				var _2ndChrStr = curStr[1];
				if (char.IsDigit(_1stChrStr))
				{// Formatted as [1234][ABCD]
					var curRow = "1234".IndexOf(_1stChrStr);
					var curCol = "abcd".IndexOf(_2ndChrStr);
					allPresses.Add(btnSelectables[4 * curRow + curCol]);
				}
				else // Formatted as [ABCD][1234]
				{
					var curRow = "1234".IndexOf(_2ndChrStr);
					var curCol = "abcd".IndexOf(_1stChrStr);
					allPresses.Add(btnSelectables[4 * curRow + curCol]);
				}
            }
			if (allPresses.Any())
				yield return null;
			foreach (var aPress in allPresses)
            {
				aPress.OnInteract();
				yield return new WaitForSeconds(0.1f);
            }
        }
		yield break;
   }

   IEnumerator TwitchHandleForcedSolve () {
		var selectedGrouping = groupings[groups];
		var lastStrikeCount = Bomb.GetStrikes();
		setValidButtons(); // Since the buttons are not adjusted for 3n+1 or 3n+2 strikes, recalculate it before autosolving.
		while (validButtons.Count(a => a != -1) > 1)
        {
			if (lastStrikeCount != Bomb.GetStrikes()) // Check if the strike count changes mid auto-solve. If it does, recalculate before continuing.
			{
				lastStrikeCount = Bomb.GetStrikes();
				setValidButtons();
			}
			for (var x = 0; x < validButtons.Length; x++)
			{
				if (validButtons[x] != -1)
				{
					btnSelectables[validButtons[x]].OnInteract();
					yield return new WaitForSeconds(0.1f);
					if (lastStrikeCount != Bomb.GetStrikes()) // Check if the strike count changes mid auto-solve. If it does, stop processing this section and recalculate.
						break;
				}
			}
        }

		yield break;
   }
}
