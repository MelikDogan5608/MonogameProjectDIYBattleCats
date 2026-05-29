using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;

namespace DIYBattleCats
{
    public enum Side { Player, Enemy }

    // unit class
    public class Unit
    {
        public Vector2 Position;
        public Side Side;
        public float Speed = 80f;
        public int Health = 50;
        public int Damage = 15;
        private float _attackTimer = 0;
        private float _attackSpeed = 1.0f;
        public bool IsDead => Health <= 0;
        public bool IsAttacking = false;

        public Unit(Vector2 startPos, Side side)
        {
            Position = startPos;
            Side = side;
        }

        public void Update(GameTime gameTime, List<Unit> targets, ref int baseHealth)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            IsAttacking = false;

            // fight test
            foreach (var target in targets)
            {
                if (target.Side != this.Side && Vector2.Distance(this.Position, target.Position) < 35)
                {
                    IsAttacking = true;
                    PerformAttack(target, dt);
                    break; 
                }
            }

            // tower is under attack test
            if (!IsAttacking)
            {
                if ((Side == Side.Player && Position.X < 70) || (Side == Side.Enemy && Position.X > 730))
                {
                    IsAttacking = true;
                    baseHealth -= Damage;
                    Health = 0; // unit sacrifices itself when hitting the base
                }
            }

            // movement
            if (!IsAttacking)
            {
                Position.X += (Side == Side.Enemy ? 1 : -1) * Speed * dt;
            }
        }

        private void PerformAttack(Unit target, float dt)
        {
            _attackTimer += dt;
            if (_attackTimer >= _attackSpeed)
            {
                target.Health -= this.Damage;
                _attackTimer = 0;
            }
        }
    }

    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        enum GameState { Menu, Settings, Playing, MapSelection, Battle, GameOver } // Добавили GameOver
        GameState currentState = GameState.Menu;

        // object variables
        Texture2D background, planroomBackground, mapBackgroundTex, battleBackgroundTex;
        Texture2D playButton, settingsButton, exitButton, settingsBackground, settingsMenu, pixel;
        Texture2D mapButtonTex, emeraldBarTex, moneyBarTex, levelTex;
        
        Texture2D[] battleBacks = new Texture2D[5];
        Rectangle mapRect, emeraldRect, moneyRect, playRect, settingsRect, exitRect, musicBar, musicKnob, soundBar, soundKnob;
        Rectangle[] levelRects = new Rectangle[5];
        int emeraldAmount = 10, moneyAmount = 1000;
        float musicVolume = 0.5f, soundVolume = 0.5f;
        bool draggingMusic = false, draggingSound = false;
        MouseState previousMouse;

        Song music; 
        SoundEffect buttonSound; 

        // gameplay variables
        private List<Unit> _battleUnits = new List<Unit>();
        private float _battleGold = 100;
        private float _goldPassiveRate = 5f;
        private int _unitCost = 30;
        private int _playerBaseHp = 500;
        private int _enemyBaseHp = 500;
        private float _enemySpawnTimer = 0;
        private KeyboardState _oldKeyState;

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

            // uploading of a content
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
                                
                                // reset game data before battle
                                _battleUnits.Clear();
                                _battleGold = 100;
                                _playerBaseHp = 500;
                                _enemyBaseHp = 500;
                                _enemySpawnTimer = 0;

                                currentState = GameState.Battle;
                                break;
                            }
                        }
                    }
                    break;

                case GameState.Battle:
                    if (keyState.IsKeyDown(Keys.Escape)) currentState = GameState.MapSelection;

                    // logic of a fight
                    _battleGold += _goldPassiveRate * dt;

                    // press 1 to buy unit
                    if (keyState.IsKeyDown(Keys.D1) && _oldKeyState.IsKeyUp(Keys.D1) && _battleGold >= _unitCost)
                    {
                        _battleUnits.Add(new Unit(new Vector2(730, 350), Side.Player));
                        _battleGold -= _unitCost;
                    }

                    // enemy spawn
                    _enemySpawnTimer += dt;
                    if (_enemySpawnTimer > 4.0f)
                    {
                        _battleUnits.Add(new Unit(new Vector2(70, 350), Side.Enemy));
                        _enemySpawnTimer = 0;
                    }

                    // unit upgrades
                    for (int i = 0; i < _battleUnits.Count; i++)
                    {
                        ref int targetBaseHp = ref (_battleUnits[i].Side == Side.Player ? ref _enemyBaseHp : ref _playerBaseHp);
                        _battleUnits[i].Update(gameTime, _battleUnits, ref targetBaseHp);
                    }

                    // reward for a kill
                    foreach (var u in _battleUnits)
                    {
                        if (u.IsDead && u.Side == Side.Enemy) _battleGold += 15;
                    }
                    _battleUnits.RemoveAll(u => u.IsDead);

                    // checking the end of the battle
                    if (_playerBaseHp <= 0 || _enemyBaseHp <= 0)
                    {
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
                    else draggingMusic = draggingSound = false;

                    if (draggingMusic) { musicVolume = MathHelper.Clamp((pos.X - musicBar.X) / (float)musicBar.Width, 0f, 1f); MediaPlayer.Volume = musicVolume; }
                    if (draggingSound) { soundVolume = MathHelper.Clamp((pos.X - soundBar.X) / (float)soundBar.Width, 0f, 1f); SoundEffect.MasterVolume = soundVolume; }
                    UpdateKnobs();
                    break;

                case GameState.GameOver:
                    if (keyState.IsKeyDown(Keys.Enter)) currentState = GameState.MapSelection;
                    break;
            }
            _oldKeyState = keyState;
            previousMouse = mouse;
            base.Update(gameTime);
        }

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
                // 1. draw a background
                _spriteBatch.Draw(battleBackgroundTex, GraphicsDevice.Viewport.Bounds, Color.White);

                // 2. draw game objects over the background
                // ground
                _spriteBatch.Draw(pixel, new Rectangle(0, 380, 800, 100), Color.DarkOliveGreen * 0.5f);

                // calculating tower heights based on their hp
                int pBaseHeight = (int)(100 * (_playerBaseHp / 500f));
                int eBaseHeight = (int)(100 * (_enemyBaseHp / 500f));

                // towers
                _spriteBatch.Draw(pixel, new Rectangle(0, 380 - eBaseHeight, 70, eBaseHeight), Color.Maroon);
                _spriteBatch.Draw(pixel, new Rectangle(730, 380 - pBaseHeight, 70, pBaseHeight), Color.DarkGreen);

                // Drawing of a player units
                foreach (var unit in _battleUnits)
                {
                    Color c = (unit.Side == Side.Player) ? Color.LimeGreen : Color.HotPink;
                    if (unit.IsAttacking) c = Color.White;
                    _spriteBatch.Draw(pixel, new Rectangle((int)unit.Position.X, (int)unit.Position.Y, 30, 30), c);
                }

                // gold indicator
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
                GraphicsDevice.Clear(Color.DarkRed);
                // drawing the end-of-game inscription
                DrawNumber(0000, 350, 200, Color.White); // temporary placeholder instead of text game over
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