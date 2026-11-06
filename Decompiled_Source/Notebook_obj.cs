using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Notebook_obj : DynamicObject
{
	public GameObject gui;

	public List<Sprite> notes = new List<Sprite>();

	public int currentNoteID;

	public Image currentNote;

	public void NextNote()
	{
		currentNoteID++;
		currentNoteID = Mathf.Clamp(currentNoteID, 0, notes.Count - 1);
		currentNote.sprite = notes[currentNoteID];
	}

	public void PrevNote()
	{
		currentNoteID--;
		currentNoteID = Mathf.Clamp(currentNoteID, 0, notes.Count - 1);
		currentNote.sprite = notes[currentNoteID];
	}

	public void Exit()
	{
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		gui.SetActive(false);
		FPSController.instance.SetPlayerDisableState("FULL");
	}

	public override void Action()
	{
		gui.SetActive(true);
		Cursor.lockState = (CursorLockMode)0;
		Cursor.visible = true;
		WeaponManager.instance.HideCurrentWeapon();
		FPSController.instance.SetPlayerDisableState("SCREEMER");
	}
}
