<img width="348" height="528" alt="ProMatch" src="https://github.com/user-attachments/assets/2afdf928-7632-42a9-bbed-6e24f7278e2a" />


# Blokfit

Blokfit is a 2D puzzle game built in Unity where the player drags and drops shaped pieces onto a triangular grid to fill it completely. Every cell is split diagonally into two triangles, giving pieces more organic shapes compared to traditional block puzzles.

## Procedural Generation

Levels are generated on the fly using a BFS flood-fill algorithm that works directly on the triangle grid. Starting from a seed triangle, it expands through adjacency rules — lower and upper triangles connect to each other following diagonal constraints — until it reaches a target size. A constraint layer then merges regions that are too small or too numerous to fit the difficulty settings. All difficulty parameters are driven by ScriptableObjects so they can be tuned without touching code.

## What I Learned

This project taught me how much a good data representation simplifies everything downstream — encoding each triangle as a single flat integer made the BFS logic, JSON format, and offset calculations all significantly cleaner. I also focused on keeping systems decoupled through C# events and the Command pattern, which made undo trivial to implement and the codebase easy to reason about. Keeping the core algorithm as pure C# with no Unity dependency meant I could cover it with unit tests and catch edge cases early without ever entering Play Mode.
