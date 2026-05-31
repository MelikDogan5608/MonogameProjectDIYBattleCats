using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DIYBattleCats
{
    public enum Side { Player, Enemy }

    public class Unit
    {
        public Vector2 Position;
        public Side Side;
        public float Speed;
        public int Health;
        public int Damage;
        private float _attackTimer = 0;
        private float _attackSpeed = 1.0f;
        public bool IsDead => Health <= 0;
        public bool IsAttacking = false;
        public bool IsInQueue = false;

        public int PendingDamage = 0;

        public Unit(Vector2 startPos, Side side, int level)
        {
            Position = startPos; 
            Side = side;
            Speed = 80f;
            Health = 5;  
            Damage = 10; 
        }

        public void UpdateLogic(GameTime gameTime, Unit enemyLeader, Unit allyAhead, bool isFrontLeader)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            if (IsDead)
            {
                IsAttacking = false;
                IsInQueue = false;
                return;
            }

            // 1. ATTACK LOGIC (Only for the front leader, if the enemy is within range)
            if (isFrontLeader && enemyLeader != null && Vector2.Distance(this.Position, enemyLeader.Position) < 35)
            {
                IsAttacking = true;
                IsInQueue = false;

                _attackTimer += dt;
                if (_attackTimer >= _attackSpeed)
                {
                    enemyLeader.PendingDamage += this.Damage;
                    _attackTimer = 0;
                }
            }
            // 2. QUEUE LOGIC (For everyone else). 
            // The unit joins the queue ONLY if it has physically reached the ally walking in front of it!
            else if (!isFrontLeader && allyAhead != null)
            {
                IsAttacking = false;
                _attackTimer = 0;

                if (Side == Side.Player)
                {
                    // If the player has caught up with the comrade in front (they are to the left of them within attack distance)
                    if (this.Position.X <= allyAhead.Position.X + 35)
                    {
                        IsInQueue = true;
                        this.Position.X = allyAhead.Position.X + 35; // Adjust position to be flush against them
                    }
                    else
                    {
                        IsInQueue = false; // Has not reached them yet, continues running
                    }
                }
                else // For enemies
                {
                    // If the enemy has caught up with their comrade (they are to the right of them)
                    if (this.Position.X >= allyAhead.Position.X - 35)
                    {
                        IsInQueue = true;
                        this.Position.X = allyAhead.Position.X - 35;
                    }
                    else
                    {
                        IsInQueue = false;
                    }
                }
            }
            else
            {
                IsAttacking = false;
                IsInQueue = false;
                _attackTimer = 0;
            }

            // Move forward only if not attacking and not standing in queue
            if (!IsAttacking && !IsInQueue)
            {
                Position.X += (Side == Side.Enemy ? 1 : -1) * Speed * dt;
            }
        }

        public void ApplyDamage()
        {
            Health -= PendingDamage;
            PendingDamage = 0;
        }
    }

    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        enum GameState { Menu, Settings, Playing, MapSelection, Battle, GameOver }
        GameState currentState = GameState.Menu;

        Texture2D background, planroomBackground, mapBackgroundTex, battleBackgroundTex;
        Texture2D playButton, settingsButton, exitButton, settingsBackground, settingsMenu, pixel;
        Texture2D mapButtonTex, emeraldBarTex, moneyBarTex, levelTex;
        Texture2D winScreenTex, loseScreenTex;
        bool _isPlayerWinner = false;
        Texture2D[] battleBacks = new Texture2D[5];
        Rectangle mapRect, emeraldRect, moneyRect, playRect, settingsRect, exitRect, musicBar, musicKnob, soundBar, soundKnob;
        Rectangle[] levelRects = new Rectangle[5];
        int emeraldAmount = 10, moneyAmount = 1000;
        float musicVolume = 0.5f, soundVolume = 0.5f;
        bool draggingMusic = false, draggingSound = false;
        MouseState previousMouse;

        Song music; 
        SoundEffect buttonSound; 

        private List<Unit> _battleUnits = new List<Unit>();
        private float _battleGold = 100;
        private float _goldPassiveRate = 6f; 
        private int _unitCost = 30;
        private int _playerBaseHp = 500;
        private int _enemyBaseHp = 500;
        private float _enemySpawnTimer = 0;
        private KeyboardState _oldKeyState;
        private int _currentLevel = 0;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize() => base.Initialize();

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            background = Content.Load<Texture2D>("background");
            planroomBackground = Content.Load<Texture2D>("planroom_Background");
            mapBackgroundTex = Content.Load<Texture2D>("Map");
            playButton = Content.Load<Texture2D>("play_Button_2");
            settingsButton = Content.Load<Texture2D>("settings_Button");
            exitButton = Content.Load<Texture2D>("exit_Button");
            settingsBackground = Content.Load<Texture2D>("settings_Background");
            settingsMenu = Content.Load<Texture2D>("settings_Menu");
            mapButtonTex = Content.Load<Texture2D>("map_Button");
            emeraldBarTex = Content.Load<Texture2D>("emrald_Bar");
            moneyBarTex = Content.Load<Texture2D>("money_Bar");    
            levelTex = Content.Load<Texture2D>("map_lavel1");
            winScreenTex = Content.Load<Texture2D>("WinScreen");
            loseScreenTex = Content.Load<Texture2D>("LoseScreen");

            battleBacks[0] = Content.Load<Texture2D>("background_level1");
            battleBacks[1] = Content.Load<Texture2D>("background_level2");
            battleBacks[2] = Content.Load<Texture2D>("background_level3");
            battleBacks[3] = Content.Load<Texture2D>("background_level4");
            battleBacks[4] = Content.Load<Texture2D>("background_level5");

            music = Content.Load<Song>("Music");
            buttonSound = Content.Load<SoundEffect>("button_Sound"); 
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = musicVolume;
            MediaPlayer.Play(music);

            int w = GraphicsDevice.Viewport.Width, h = GraphicsDevice.Viewport.Height;
            playRect = new Rectangle(w / 2 - 110, h / 2 - 130, 220, 70);
            settingsRect = new Rectangle(w / 2 - 110, h / 2 - 35, 220, 70);
            exitRect = new Rectangle(w / 2 - 110, h / 2 + 60, 220, 70);

            mapRect = new Rectangle(220, 200, 220, 70);
            emeraldRect = new Rectangle(w - 390, 20, 180, 50);
            moneyRect = new Rectangle(w - 200, 20, 180, 50);

            musicBar = new Rectangle(w / 2 - 100, h / 2 - 20, 200, 10);
            soundBar = new Rectangle(w / 2 - 100, h / 2 + 60, 200, 10);
            UpdateKnobs();

            levelRects[0] = new Rectangle(130, 195, 45, 45);
            levelRects[1] = new Rectangle(245, 295, 45, 45);
            levelRects[2] = new Rectangle(395, 175, 45, 45);
            levelRects[3] = new Rectangle(520, 240, 45, 45);
            levelRects[4] = new Rectangle(580, 150, 45, 45);
        }

        void UpdateKnobs()
        {
            musicKnob = new Rectangle((int)(musicBar.X + musicVolume * musicBar.Width) - 5, musicBar.Y - 10, 10, 30);
            soundKnob = new Rectangle((int)(soundBar.X + soundVolume * soundBar.Width) - 5, soundBar.Y - 10, 10, 30);
        }

        protected override void Update(GameTime gameTime)
        {
            MouseState mouse = Mouse.GetState();
            KeyboardState keyState = Keyboard.GetState();
            Point pos = mouse.Position;
            bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            switch (currentState)
            {
                case GameState.Menu:
                    if (clicked && playRect.Contains(pos)) { buttonSound?.Play(); currentState = GameState.Playing; }
                    if (clicked && settingsRect.Contains(pos)) { buttonSound?.Play(); currentState = GameState.Settings; }
                    if (clicked && exitRect.Contains(pos)) { buttonSound?.Play(); Exit(); }
                    break;

                case GameState.Playing:
                    if (keyState.IsKeyDown(Keys.Escape)) currentState = GameState.Menu;
                    if (clicked && mapRect.Contains(pos)) { buttonSound?.Play(); currentState = GameState.MapSelection; }
                    break;

                case GameState.MapSelection:
                    if (keyState.IsKeyDown(Keys.Escape)) currentState = GameState.Playing;
                    if (clicked)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (levelRects[i].Contains(pos))
                            {
                                buttonSound?.Play();
                                battleBackgroundTex = battleBacks[i];
                                _currentLevel = i; 

                                _battleUnits.Clear();
                                _battleGold = 100;
                                _playerBaseHp = 500;
                                _enemyBaseHp = 500;
                                _enemySpawnTimer = 0;

                                MediaPlayer.Stop(); 
                                currentState = GameState.Battle;
                                break;
                            }
                        }
                    }
                    break;

                case GameState.Battle:
                    if (keyState.IsKeyDown(Keys.Escape)) 
                    {
                        MediaPlayer.Volume = musicVolume;
                        MediaPlayer.Play(music); 
                        SoundEffect.MasterVolume = soundVolume;
                        currentState = GameState.MapSelection;
                    }

                    _battleGold += _goldPassiveRate * dt;

                    // Player spawn at base (X = 730)
                    if (keyState.IsKeyDown(Keys.D1) && _oldKeyState.IsKeyUp(Keys.D1) && _battleGold >= _unitCost)
                    {
                        _battleUnits.Add(new Unit(new Vector2(730, 350), Side.Player, _currentLevel));
                        _battleGold -= _unitCost;
                        SoundEffect.MasterVolume = 1.0f;
                        buttonSound?.Play();
                    }

                    // Enemy spawn at base (X = 70) with increased difficulty
                    _enemySpawnTimer += dt;
                    float currentSpawnInterval = MathMax(6.5f - (_currentLevel * 0.75f), 3.5f); 
                    if (_enemySpawnTimer > currentSpawnInterval)
                    {
                        _battleUnits.Add(new Unit(new Vector2(70, 350), Side.Enemy, _currentLevel));
                        _enemySpawnTimer = 0;
                        SoundEffect.MasterVolume = 1.0f;
                        buttonSound?.Play();
                    }

                    // Sort lists to correctly determine "who is in front"
                    var sortedPlayerUnits = _battleUnits.Where(u => u.Side == Side.Player).OrderBy(u => u.Position.X).ToList();
                    var sortedEnemyUnits = _battleUnits.Where(u => u.Side == Side.Enemy).OrderByDescending(u => u.Position.X).ToList();

                    Unit playerLeader = sortedPlayerUnits.FirstOrDefault();
                    Unit enemyLeader = sortedEnemyUnits.FirstOrDefault();

                    // PLAYER UNITS LOGIC UPDATE
                    for (int i = 0; i < sortedPlayerUnits.Count; i++)
                    {
                        var unit = sortedPlayerUnits[i];
                        bool isLeader = (unit == playerLeader);
                        // The one right before them in the list is the ally moving in front of them
                        Unit allyAhead = isLeader ? null : sortedPlayerUnits[i - 1]; 
                        
                        unit.UpdateLogic(gameTime, enemyLeader, allyAhead, isLeader);
                    }

                    // ENEMY UNITS LOGIC UPDATE
                    for (int i = 0; i < sortedEnemyUnits.Count; i++)
                    {
                        var unit = sortedEnemyUnits[i];
                        bool isLeader = (unit == enemyLeader);
                        Unit allyAhead = isLeader ? null : sortedEnemyUnits[i - 1];

                        unit.UpdateLogic(gameTime, playerLeader, allyAhead, isLeader);
                    }

                    // Check base attacks by leaders
                    if (playerLeader != null && playerLeader.Position.X < 70) { _enemyBaseHp -= 100; playerLeader.Health = 0; }
                    if (enemyLeader != null && enemyLeader.Position.X > 730) { _playerBaseHp -= 100; enemyLeader.Health = 0; }

                    // Apply damage at the end of the frame
                    for (int i = 0; i < _battleUnits.Count; i++)
                    {
                        _battleUnits[i].ApplyDamage();
                    }

                    // Reward and clean up list
                    foreach (var u in _battleUnits)
                    {
                        if (u.IsDead && u.Side == Side.Enemy) _battleGold += 15;
                    }
                    _battleUnits.RemoveAll(u => u.IsDead);

                    if (_playerBaseHp <= 0)
                    {
                        _isPlayerWinner = false;
                        currentState = GameState.GameOver;
                    }
                    else if (_enemyBaseHp <= 0)
                    {
                        _isPlayerWinner = true;
                        currentState = GameState.GameOver;
                    }
                    break;

                case GameState.Settings:
                    if (keyState.IsKeyDown(Keys.Escape)) currentState = GameState.Menu;
                    if (mouse.LeftButton == ButtonState.Pressed)
                    {
                        draggingMusic = musicBar.Contains(pos) || draggingMusic;
                        draggingSound = soundBar.Contains(pos) || draggingSound;
                    }
                    else draggingSound = draggingMusic = false;

                    if (draggingMusic) { musicVolume = MathHelper.Clamp((pos.X - musicBar.X) / (float)musicBar.Width, 0f, 1f); MediaPlayer.Volume = musicVolume; }
                    if (draggingSound) { soundVolume = MathHelper.Clamp((pos.X - soundBar.X) / (float)soundBar.Width, 0f, 1f); SoundEffect.MasterVolume = soundVolume; }
                    UpdateKnobs();
                    break;

                case GameState.GameOver:
                    if (keyState.IsKeyDown(Keys.Enter))
                    {
                        MediaPlayer.Volume = musicVolume;
                        MediaPlayer.Play(music);
                        SoundEffect.MasterVolume = soundVolume;
                        currentState = GameState.MapSelection;
                    }
                    break;
            }
            _oldKeyState = keyState;
            previousMouse = mouse;
            base.Update(gameTime);
        }

        private float MathMax(float a, float b) => a > b ? a : b;

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin();

            if (currentState == GameState.Menu)
            {
                _spriteBatch.Draw(background, GraphicsDevice.Viewport.Bounds, Color.White);
                DrawButton(playButton, playRect); DrawButton(settingsButton, settingsRect); DrawButton(exitButton, exitRect);
            }
            else if (currentState == GameState.Playing)
            {
                _spriteBatch.Draw(planroomBackground, GraphicsDevice.Viewport.Bounds, Color.White);
                DrawButton(mapButtonTex, mapRect);
                _spriteBatch.Draw(pixel, new Rectangle(emeraldRect.X + 52, emeraldRect.Y + 12, 112, 26), Color.LimeGreen * 0.4f);
                _spriteBatch.Draw(pixel, new Rectangle(moneyRect.X + 52, moneyRect.Y + 12, 112, 26), Color.Gold * 0.4f);
                _spriteBatch.Draw(emeraldBarTex, emeraldRect, Color.White); _spriteBatch.Draw(moneyBarTex, moneyRect, Color.White);
                DrawNumber(emeraldAmount, emeraldRect.X + 85, emeraldRect.Y + 16, Color.White); DrawNumber(moneyAmount, moneyRect.X + 80, moneyRect.Y + 16, Color.White);
            }
            else if (currentState == GameState.MapSelection)
            {
                _spriteBatch.Draw(mapBackgroundTex, GraphicsDevice.Viewport.Bounds, Color.White);
                for (int i = 0; i < 5; i++) DrawButton(levelTex, levelRects[i]);
            }
            else if (currentState == GameState.Battle && battleBackgroundTex != null)
            {
                _spriteBatch.Draw(battleBackgroundTex, GraphicsDevice.Viewport.Bounds, Color.White);

                int pBaseHeight = (int)(100 * (_playerBaseHp / 500f));
                int eBaseHeight = (int)(100 * (_enemyBaseHp / 500f));

                _spriteBatch.Draw(pixel, new Rectangle(0, 380 - eBaseHeight, 70, eBaseHeight), Color.Maroon);
                _spriteBatch.Draw(pixel, new Rectangle(730, 380 - pBaseHeight, 70, pBaseHeight), Color.DarkGreen);

                Unit activePlayerLeader = _battleUnits.Where(u => u.Side == Side.Player).OrderBy(u => u.Position.X).FirstOrDefault();
                Unit activeEnemyLeader = _battleUnits.Where(u => u.Side == Side.Enemy).OrderByDescending(u => u.Position.X).FirstOrDefault();

                int playerStackIndex = 0;
                int enemyStackIndex = 0;

                var renderPlayerUnits = _battleUnits.Where(u => u.Side == Side.Player).OrderBy(u => u.Position.X).ToList();
                var renderEnemyUnits = _battleUnits.Where(u => u.Side == Side.Enemy).OrderBy(u => u.Position.X).ToList();

                foreach (var unit in renderPlayerUnits)
                {
                    Color c = Color.LimeGreen;
                    if (unit.IsAttacking && unit == activePlayerLeader) c = Color.White;

                    int renderX = (int)unit.Position.X;
                    int renderY = (int)unit.Position.Y;

                    if (unit.IsAttacking || unit.IsInQueue)
                    {
                        renderX = (int)activePlayerLeader.Position.X;
                        renderY = (int)activePlayerLeader.Position.Y - (playerStackIndex * 35);
                        playerStackIndex++;
                    }

                    _spriteBatch.Draw(pixel, new Rectangle(renderX, renderY, 30, 30), c);
                }

                foreach (var unit in renderEnemyUnits)
                {
                    Color c = Color.HotPink;
                    if (unit.IsAttacking && unit == activeEnemyLeader) c = Color.White;

                    int renderX = (int)unit.Position.X;
                    int renderY = (int)unit.Position.Y;

                    if (unit.IsAttacking || unit.IsInQueue)
                    {
                        renderX = (int)activeEnemyLeader.Position.X;
                        renderY = (int)activeEnemyLeader.Position.Y - (enemyStackIndex * 35);
                        enemyStackIndex++;
                    }

                    _spriteBatch.Draw(pixel, new Rectangle(renderX, renderY, 30, 30), c);
                }

                _spriteBatch.Draw(pixel, new Rectangle(10, 10, 150, 40), Color.Black * 0.6f);
                DrawNumber((int)_battleGold, 20, 20, Color.Gold);
            }
            else if (currentState == GameState.Settings)
            {
                _spriteBatch.Draw(settingsBackground, GraphicsDevice.Viewport.Bounds, Color.White);
                _spriteBatch.Draw(settingsMenu, new Rectangle(GraphicsDevice.Viewport.Width / 2 - 150, GraphicsDevice.Viewport.Height / 2 - 150, 300, 300), Color.White);
                _spriteBatch.Draw(pixel, musicBar, Color.Gray); _spriteBatch.Draw(pixel, musicKnob, Color.Blue);
                _spriteBatch.Draw(pixel, soundBar, Color.Gray); _spriteBatch.Draw(pixel, soundKnob, Color.Red);
            }
            else if (currentState == GameState.GameOver)
            {
                if (_isPlayerWinner)
                {
                    _spriteBatch.Draw(winScreenTex, GraphicsDevice.Viewport.Bounds, Color.White);
                }
                else
                {
                    _spriteBatch.Draw(loseScreenTex, GraphicsDevice.Viewport.Bounds, Color.White);
                }
            }

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        void DrawButton(Texture2D tex, Rectangle rect) => _spriteBatch.Draw(tex, rect, rect.Contains(Mouse.GetState().Position) ? Color.LightGray : Color.White);
        
        void DrawNumber(int number, int startX, int startY, Color color)
        {
            string numStr = number.ToString(); int currentX = startX, scale = 3;
            string[] raw = { "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001", "111100111001111", "111100111101111", "111001010010010", "111101111101111", "111101111001111" };
            foreach (char c in numStr)
            {
                int idx = c - '0'; if (idx < 0 || idx > 9) continue;
                for (int i = 0; i < 15; i++) if (raw[idx][i] == '1') _spriteBatch.Draw(pixel, new Rectangle(currentX + (i % 3 * scale), startY + (i / 3 * scale), scale, scale), color);
                currentX += 4 * scale;
            }
        }
    }
}