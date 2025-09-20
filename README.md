project is in unity you can try it on browser here: <a>https://ratweeb.itch.io/odm-gear


# Attack on Titan ODM Gear Simulator 

A Unity-based simulation game featuring the iconic ODM (Omnidirectional Mobility) gear from Attack on Titan anime/manga series.

##  How to Start Developing

### Prerequisites

Before you begin, make sure you have the following installed on your system:

- **Unity Hub** - [Download here](https://unity.com/download)
- **Git** - [Download here](https://git-scm.com/downloads)
- **Visual Studio** or **Visual Studio Code** - [VS Download](https://visualstudio.microsoft.com/) | [VS Code Download](https://code.visualstudio.com/)

### Step 1: Download Unity 2022.3.25f1

1. Open **Unity Hub**
2. Go to the **Installs** tab
3. Click **Install Editor**
4. Select **Archive** tab
5. Find and download **Unity 2022.3.25f1** (LTS)
   - Make sure to include **Visual Studio** integration
   - Include **Windows Build Support** (and other platforms if needed)

> ⚠️ **Important**: This project requires Unity 2022.3.25f1 specifically. Other versions may cause compatibility issues.

### Step 2: Clone the Repository

Open your terminal/command prompt and run:

```bash
git clone https://github.com/[YOUR-USERNAME]/attack-on-titan-odm-sim.git
cd attack-on-titan-odm-sim
```

### Step 3: Open Project in Unity

1. Open **Unity Hub**
2. Click **Open** or **Add**
3. Navigate to the cloned repository folder
4. Select the project folder and click **Open**
4. Unity will automatically import all assets (this may take a few minutes)

### Step 4: Install Dependencies

The project may use Unity packages. If prompted:

1. Go to **Window > Package Manager**
2. Install any missing packages that appear in the console
3. Common packages used might include:
   - Input System
   - Cinemachine
   - Universal Render Pipeline (URP)

### Step 5: Set Up Your Development Environment

1. **Configure Version Control**:
   ```bash
   git config user.name "Your Name"
   git config user.email "your.email@example.com"
   ```

2. **Create a new branch for your work**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Test the project**:
   - Press **Play** in Unity Editor
   - Verify the game runs without errors

## 🎮 Game Controls (Default)

- **WASD** - Character movement
- **Mouse** - Camera look
- **Left Click** - Fire left ODM hook
- **Right Click** - Fire right ODM hook
- **Space** - Boost/Gas burst
- **Shift** - Sprint (on ground)

## 📁 Project Structure

```
Assets/
├── Scripts/           # C# game logic
├── Models/           # 3D models and meshes
├── Materials/        # Materials and textures
├── Animations/       # Character and object animations
├── Prefabs/          # Reusable game objects
├── Scenes/           # Unity scenes
├── Audio/            # Sound effects and music
└── UI/               # User interface elements
```

## 🔧 Development Guidelines

### Before Making Changes

1. Always pull the latest changes:
   ```bash
   git pull origin main
   ```

2. Create a new branch for your feature:
   ```bash
   git checkout -b feature/feature-name
   ```

### Making Commits

1. Stage your changes:
   ```bash
   git add .
   ```

2. Commit with a descriptive message:
   ```bash
   git commit -m "Add: ODM gear physics improvements"
   ```

3. Push to your branch:
   ```bash
   git push origin feature/feature-name
   ```

### Submitting Changes

1. Create a **Pull Request** on GitHub
2. Describe your changes clearly
3. Wait for code review before merging

## 🐛 Common Issues & Solutions

### Issue: Unity version mismatch
**Solution**: Make sure you're using Unity 2022.3.25f1 exactly

### Issue: Missing packages
**Solution**: Check Package Manager and install any missing dependencies

### Issue: Script compilation errors
**Solution**: Check console for specific errors and ensure all dependencies are installed

### Issue: Git LFS files not downloading
**Solution**: Make sure Git LFS is installed and run:
```bash
git lfs pull
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

### Code Style

- Use **PascalCase** for public methods and properties
- Use **camelCase** for private fields and variables
- Add comments for complex logic
- Follow Unity's coding conventions

## 📋 Features to Implement

- [ ] Improved ODM gear physics
- [ ] Titan AI behavior
- [ ] Multiplayer support
- [ ] Character customization
- [ ] Level editor
- [ ] Achievement system

## 🆘 Getting Help

- **Issues**: [Report bugs on GitHub](https://github.com/maxsunc/ODM-Gear-Simulation/issues)

**Happy coding, and may your ODM gear never fail! ⚔️🕷️**
