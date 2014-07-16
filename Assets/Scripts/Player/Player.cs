﻿using UnityEngine;
using System.Collections;

public class Player : MonoBehaviour {
    public const float RESPAWN_COOLDOWN = 3.0f;
    public const float PLAYER_LEVEL_TRANSITION_TIME = 1.5f;
    public const float CAMERA_LEVEL_TRANSITION_TIME = 1.75f;
    public const float FLASHBANG_TIME = 0.75f;
    public const float DELAY_NEXT_SHOT = .17f;
    public const float RAPID_SHOT_TIME = 15.0f;
    public const float MULTI_SHOT_TIME = 15.0f;

    private const float CAMERA_PERCENT_BACK = .7f;
    private float _mouseSensitivity;

    public GameObject TestLevel;
    public Level currentLevel;

    private int _currentPlane = 0;
    private float _positionOnPlane = 0.5f; // between 0 (beginning) and 1 (end)
    
    private bool _alive = false;
    public int maxShots = 5;
    private int _numShots = 0;
    private float _reload = 0;

    public GameObject playerProjectile;

    private float _rapidTime = 0;
    private float _multiTime = 0;

    public bool isRapidActivated { get; private set; }
    public bool isMultiActivated { get; private set; }
    public bool isCloneActivated { get; private set; }
    public bool isClone { get; private set; }// is this ship a clone?
    private bool _isMovementMirrored = false; // does the ship move in reverse?

    private Player _clone; // the player's clone
    public GameObject cloneObject; // the clone object

    private EscapeMenu _escapeMenu;

    //Flying variables
    private bool _transitioning = false;
    private float _startTransTime;
    private Vector3 _startPos;
    private Vector3 _cameraStartPos;
    private AudioClip _deathSound;
    private AudioClip _shootSound;
    private AlpacaSound.RetroPixel _retroPixelShader;
    private Flashbang _flashbang;
    private bool _invulnerable = false;
    private GameObject _previousLevel;
    private float _invulnerabilityCooldown;

    // Use this for initialization
    void Start () {
        isRapidActivated = isMultiActivated = isCloneActivated = false;
        if (TestLevel != null) {
            currentLevel = TestLevel.GetComponent<Level>();
            init(currentLevel);
        }
        _mouseSensitivity = GameManager.mouseSensitivity;
        if (_mouseSensitivity < .1f) {
            _mouseSensitivity = .1f;
        }
        GameManager.Instance.addShip(this);
        gameObject.rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        _deathSound = (AudioClip)Resources.Load("Sound/ShipExplode");
        _shootSound = (AudioClip)Resources.Load("Sound/PlayerShoot");
    }

    public void UpdateSensitivity()
    {
        _mouseSensitivity = GameManager.mouseSensitivity;
    }

    public void loadNextLevel(Level level) {
        _transitioning = true;
        _invulnerable = false;
        _invulnerabilityCooldown = Time.time - RESPAWN_COOLDOWN;
        _previousLevel = currentLevel.gameObject;
        currentLevel.lanes[_currentPlane].setHighlight(false);
        _startTransTime = Time.time;
        currentLevel = level;
        _startPos = gameObject.transform.position;
        _cameraStartPos = CameraController.currentCamera.gameObject.transform.position;
    }

    public bool isTransitioning {
        get {
            return _transitioning;
        }
    }

    public void init(Level level) {
        if (isClone) {
            return;
        }
        currentLevel = level;
        CameraController.currentCamera.gameObject.transform.position = currentLevel.cameraPosition.transform.position;
        _currentPlane = currentLevel.lanes.IndexOf(currentLevel.SpawnLane);
        _alive = true;
        _invulnerable = true;
        _invulnerabilityCooldown = Time.time;
        _escapeMenu = gameObject.GetComponent<EscapeMenu>();
    }

    public void initAsClone(Level level, int plane, float position) {
        isClone = true;
        currentLevel = level;
        _alive = true;
        // initialize clone location
        if (!currentLevel.wrapAround) { // level doesn't wrap
            _isMovementMirrored = true;
            _currentPlane = level.lanes.Count - 1 - plane;
            _positionOnPlane = 1 - position;
        }
        else {
            _currentPlane = plane - (level.lanes.Count / 2);
            if (_currentPlane < 0) {
                _currentPlane += level.lanes.Count;
            }
            _positionOnPlane = position;
        }
        _escapeMenu = GameManager.Instance.CurrentPlayerShips[0].GetComponent<EscapeMenu>();
    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown(KeyCode.Escape) && !isClone) {
            if (!_escapeMenu.currentlyActive) {
                _escapeMenu.display();
            }
        }
        if (_escapeMenu.currentlyActive) {
            return;
        }
        Screen.lockCursor = true;

        if (_invulnerable) {
            renderer.enabled = Mathf.Sin(Time.time * 50.0f) > 0;
        }
        if (Time.time > _invulnerabilityCooldown + RESPAWN_COOLDOWN) {
            renderer.enabled = true;
            _invulnerable = false;
        }
        if (_transitioning && currentLevel.SpawnLane != null) {
            if (isClone) {
                GameManager.Instance.removeShip(this);
                Destroy(gameObject);
            }
            setCamera();
            gameObject.transform.position = Vector3.Lerp(_startPos, currentLevel.SpawnLane.Front, (Time.time - _startTransTime) / PLAYER_LEVEL_TRANSITION_TIME);
            gameObject.transform.Rotate(new Vector3(1, 0, 0), 360.0f * (Time.time - _startTransTime) / PLAYER_LEVEL_TRANSITION_TIME);
            if (gameObject.transform.position == currentLevel.SpawnLane.Front && 
                CameraController.currentCamera.gameObject.transform.position == currentLevel.cameraPosition.transform.position) {
                    if (!_flashbang.running) {
                        _flashbang.init(FLASHBANG_TIME);
                    }
                    if ((Time.time - _startTransTime) / (PLAYER_LEVEL_TRANSITION_TIME + FLASHBANG_TIME / 2.0f) >= 1.0f) {
                        _retroPixelShader.enabled = false;
                    }
                    if (_flashbang.manualUpdate()) {
                        return;
                    }
                    _retroPixelShader.enabled = false;
                    _transitioning = false;
                    Destroy(_previousLevel);
                    if (currentLevel.SpawnLane != null) {
                        _currentPlane = currentLevel.lanes.IndexOf(currentLevel.SpawnLane);
                    } else {
                        _currentPlane = 0;
                    }
            }
            return;
        }
        if (currentLevel == null || currentLevel.lanes == null) {
            return;
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space)) { // testing purposes
            ActivateClone();
            ActivateMulti();
            ActivateRapid();
        }

        if (Input.GetKeyDown(KeyCode.F1)) {
            _invulnerable = !_invulnerable;
            _invulnerabilityCooldown = (_invulnerable) ? float.MaxValue : 0;
            GUIManager.Instance.addGUIItem(new GUIItem(Screen.width/2,Screen.height/2,"God Mode : " + _invulnerable.ToString(),GUIManager.Instance.defaultStyle,4));
        }
#endif

        if (!_alive) {
            return;
        }
        float mouseMove = Input.GetAxis("Mouse X");
        float shipMove = mouseMove * _mouseSensitivity;
        if (_isMovementMirrored) {
            _positionOnPlane -= shipMove;
        }
        else {
            _positionOnPlane += shipMove;
        }

        currentLevel.lanes[_currentPlane].setHighlight(false);
        // calculate new position after movement
        if (_positionOnPlane < 0) {
            _currentPlane--;
            _positionOnPlane++;
        }
        else if (_positionOnPlane > 1) {
            _currentPlane++;
            _positionOnPlane--;
        }

        if (_currentPlane < 0) {
            if (currentLevel.wrapAround) {
                _currentPlane += currentLevel.lanes.Count;
            }
            else {
                _currentPlane = 0;
                _positionOnPlane = 0;
            }
        }
        else if (_currentPlane >= currentLevel.lanes.Count) {
            if (currentLevel.wrapAround) {
                _currentPlane -= currentLevel.lanes.Count;
            }
            else {
                _currentPlane = currentLevel.lanes.Count - 1;
                _positionOnPlane = 1;
            }
        }
        currentLevel.lanes[_currentPlane].setHighlight(true);

        // update position
        transform.position = currentLevel.lanes[_currentPlane].Front;
        float angleUp = Vector3.Angle(Vector3.up, currentLevel.lanes[_currentPlane].Normal) - 90;
        float angleRight = Vector3.Angle(Vector3.forward, currentLevel.lanes[_currentPlane].Normal);
        float angleLeft = Vector3.Angle(Vector3.back, currentLevel.lanes[_currentPlane].Normal);
        if (angleRight < angleLeft) {
            angleUp = -angleUp;
        }
        transform.eulerAngles = new Vector3(angleUp, 180, 0);

        // shoot
        if (_numShots < 0)
            _numShots = 0;

        int maxMultiShots = maxShots;
        if (isMultiActivated) {
            maxMultiShots = maxShots * 3;
        }
        if (Input.GetMouseButton(0) && _reload < 0 && _numShots < maxMultiShots) {
            Shoot();
            _reload = DELAY_NEXT_SHOT;
        
        }
        
        // reload
        _reload -= Time.deltaTime;

        // powerup timers
        if (_rapidTime < 0) {
            isRapidActivated = false;
        }
        else {
            _rapidTime -= Time.deltaTime;
        }

        if (_multiTime < 0) {
            isMultiActivated = false;
        }
        else {
            _multiTime -= Time.deltaTime;
        }

        if (_clone == null) {
            isCloneActivated = false;
        }
    }

    private void setCamera() {
        if (_retroPixelShader == null) {
            _retroPixelShader = CameraController.currentCamera.gameObject.GetComponent<AlpacaSound.RetroPixel>();
            _flashbang = CameraController.currentCamera.gameObject.GetComponent<Flashbang>();
        }
        _retroPixelShader.enabled = true;
        float percent = (Time.time - _startTransTime) / CAMERA_LEVEL_TRANSITION_TIME;
        CameraController.currentCamera.gameObject.transform.position = Vector3.Lerp(_cameraStartPos, currentLevel.cameraPosition.transform.position,percent);
        _retroPixelShader.verticalResolution = (int)(percent * Screen.height);
        _retroPixelShader.horizontalResolution = (int)(percent * Screen.width);
    }
    
    public Lane CurrentLane {
        get {
            if(currentLevel != null && currentLevel.lanes != null && _currentPlane < currentLevel.lanes.Count) {
                return currentLevel.lanes[_currentPlane];
            } else {
                return null;
            }
        }
    }

    void Shoot() {
        AudioSource.PlayClipAtPoint(_shootSound, transform.position, GameManager.effectsVolume);
        Lane currentLane = currentLevel.lanes[_currentPlane];
        GameObject shot = (GameObject)Instantiate(playerProjectile);
        shot.renderer.enabled = false;
        shot.GetComponent<PlayerProjectile>().init(currentLane,this);
        _numShots++;
        if (isMultiActivated) {
            currentLane = currentLevel.lanes[_currentPlane].LeftLane;
            if (currentLane != null) {
                shot = (GameObject)Instantiate(playerProjectile);
                shot.GetComponent<PlayerProjectile>()._player = this;
                shot.GetComponent<PlayerProjectile>().init(currentLane,this);
                _numShots++;
            }
            currentLane = currentLevel.lanes[_currentPlane].RightLane;
            if (currentLane != null) {
                shot = (GameObject)Instantiate(playerProjectile);
                shot.GetComponent<PlayerProjectile>()._player = this;
                shot.GetComponent<PlayerProjectile>().init(currentLane,this);
                _numShots++;
            }
        }
    }
    
    public void RemoveShot() {
        _numShots--;
    }
    
    void OnTriggerEnter(Collider other) {
        if (_invulnerable) {
            return;
        }
        if (other.gameObject.tag == "Enemy") {
            if (_retroPixelShader != null) {
                _retroPixelShader.enabled = false;
            }
            ParticleManager.Instance.initParticleSystem(ParticleManager.Instance.playerDeath, gameObject.transform.position);
            if (!isClone && isCloneActivated) {
                _clone.CloneBecomeMain();
            }
            AudioSource.PlayClipAtPoint(_deathSound, transform.position, GameManager.effectsVolume);
            currentLevel.lanes[_currentPlane].setHighlight(false);
            GameManager.Instance.removeShip(this);
            other.gameObject.GetComponent<Enemy>().explode();
            Destroy(gameObject);
        }
    }

    public void ActivateRapid() {
        _rapidTime = RAPID_SHOT_TIME;
        isRapidActivated = true;
        if (!isClone && isCloneActivated) {
            _clone.ActivateRapid();
        }
    }

    public void ActivateMulti() {
        _multiTime = MULTI_SHOT_TIME;
        isMultiActivated = true;
        if (!isClone && isCloneActivated) {
            _clone.ActivateMulti();
        }
    }

    public void ActivateClone() {
        if (!isClone && !isCloneActivated) {
            SpawnClone();
        }
    }

    void SpawnClone() {
        isCloneActivated = true;
        GameObject playerClone = (GameObject)Instantiate(cloneObject);
        _clone = playerClone.GetComponent<Player>();
        _clone.initAsClone(currentLevel, _currentPlane, _positionOnPlane);
    }

    public void CloneBecomeMain() {
        _isMovementMirrored = false;
        isClone = false;
        if (gameObject != null) {
            _escapeMenu = gameObject.GetComponent<EscapeMenu>();
        }
    }

}
