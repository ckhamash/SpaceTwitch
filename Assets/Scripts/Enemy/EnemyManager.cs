﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyManager : MonoBehaviour {
    private static EnemyManager _instance;

    public GameObject pawnPrefab;
    public GameObject crosshatchPrefab;
    public GameObject swirliePrefab;
    public GameObject confettiPrefab;
    public GameObject enemyProjectilePrefab;

    private List<Enemy> _currentEnemies;
    private List<Enemy> _deadEnemies;

    void Start () {
        if(_instance != null) {
            Debug.LogError("There can't be two Enemy Managers!");
        }
        _deadEnemies = new List<Enemy>();
        _instance = this;
        DontDestroyOnLoad(this);
    }

    public void debugPawn() {
        GameObject g = (GameObject)Instantiate(pawnPrefab);
        Debug.Log(g);
        g.GetComponent<Pawn>().spawn(GameManager.Instance.CurrentLevel.getRandomLane());
    }

    /**
     * Call this whenever a new level is loaded or restarted
     */
    public void loadLevel() {
        _currentEnemies.Clear();
        _deadEnemies.Clear();

        _currentEnemies = GameManager.Instance.CurrentLevel.PotentialEnemies;
    }

    public static EnemyManager Instance {
        get { 
            return _instance;
        }
    }

    void Update () {

    }
}
