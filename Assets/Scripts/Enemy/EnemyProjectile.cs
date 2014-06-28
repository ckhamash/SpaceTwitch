﻿using UnityEngine;
using System.Collections;

public class EnemyProjectile : Enemy {
    //Time it takes to get to the end of a lane
    private const float DURATION = 5.0f;
    private float _startingTime;

    void Start () {
	
    }
	
    void Update () {
        if (!Alive) {
            return;
        }
        gameObject.transform.position = Vector3.Lerp(_currentLane.Back + (gameObject.renderer.bounds.size.y / 2) * _currentLane.Normal, 
            _currentLane.Front + (gameObject.renderer.bounds.size.y / 2) * _currentLane.Normal, (Time.time - _startingTime) / DURATION);
        if (gameObject.transform.position == _currentLane.Front + (gameObject.renderer.bounds.size.y / 2) * _currentLane.Normal) {
            Destroy(gameObject);
        }
    }

    public override void spawn(Lane spawnLane) {
        _currentLane = spawnLane;
        _alive = true;
        gameObject.transform.position = _currentLane.Back + (gameObject.renderer.bounds.size.y/2) * _currentLane.Normal;
        _startingTime = Time.time;
    }
}
