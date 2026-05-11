# Folder Cleaner Utility

This project was created to solve routine problems related to deleting directories during software development. It streamlines the process of cleaning up temporary, build, or test folders.

## Use Cases

1. **Development Cleanup**: During development, test folders are often created and need to be repeatedly deleted while debugging the application.
2. **.NET Development**: Specifically useful for .NET developers who frequently need to remove standard build and configuration folders such as `.vs`, `bin`, and `obj`.

This tool allows you to select a root directory and specify which subfolders should be deleted, making the cleanup process faster and more convenient.

## Features

- **Folder Name Filtering**: Easily filter folders by name to find what you need quickly.
- **Tree View Controls**:
  - `+` Button: Expands the main directory tree completely.
  - `-` Button: Collapses the main directory tree.
- **Selection Management**:
  - **Save Button**: Saves the current configuration (selected folders). Checkboxes will persist across program restarts.
  - **Clear Button**: Unchecks all selected folders and resets the configuration file.
- **Deletion Controls**:
  - **Delete Button**: Deletes all currently selected folders.
  - **Skip Button**: Skips errors that occur during the deletion process (e.g., if a folder is locked).
- **Visual Feedback**:
  - **Right-Side Tree**: Displays a summary of folders currently selected for deletion.
  - **Bottom Log Panel**: Shows real-time logs of actions, errors, and successfully deleted folders.
- **Auto-Scanning**: The root directory is scanned every 3 seconds to detect newly created folders automatically.

## Usage Instructions

1. **Launch the Application**: Start the program and select the **root folder** containing the directories you wish to manage.
2. **Select Folders**:
   - Browse the directory tree and check the boxes next to the folders you want to delete.
   - Click the **Save** button. Your selections will be saved to a configuration file, so they will be automatically restored the next time you run the program.
3. **Delete**:
   - Click the **Delete** button to remove the selected folders.
   - Monitor the **Log Panel** at the bottom for status updates and any potential errors.

## Technical Details

- **Configuration Persistence**: Selected folders are saved in a configuration file, ensuring your preferences are remembered between sessions.
- **Real-time Monitoring**: The application continuously monitors the root directory for changes (new folders) every 3 seconds.

## Creating a publish version

# 🪟 WINDOWS x64
dotnet publish -c Release -r win-x64 -o publish/releases/FCPApp-Windows-x64

# 🪟 WINDOWS ARM64
dotnet publish -c Release -r win-arm64 -o publish/releases/FCPApp-Windows-ARM64

# 🐧 LINUX x64
dotnet publish -c Release -r linux-x64 -o publish/releases/FCPApp-Linux-x64

# 🐧 LINUX ARM64
dotnet publish -c Release -r linux-arm64 -o publish/releases/FCPApp-Linux-ARM64

# 🍎 MACOS x64
dotnet publish -c Release -r osx-x64 -o publish/releases/FCPApp-macOS-Intel

# 🍎 MACOS ARM64
dotnet publish -c Release -r osx-arm64 -o publish/releases/FCPApp-macOS-AppleSilicon