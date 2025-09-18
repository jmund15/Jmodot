# Jmodot Gemini CLI Guide

## High Level Instructions

Don't cave in immediately to worries or suggestions. Deeply dive into issues, and come to objective, best solutions & answers, based on facts and not just user input.
Explain reasoning thoroughly. 
These are tough architectural questions, so think through all use cases in scenarios before coming to a conclusion.
Similarly, this is a LARGE library. Thoroughly analyze all possible references and possiblities before coming to a conclusion.
The core principles are code scalability, robustness, and intuitiveness.


## Design Principles and Philosophy
These are are wisdom that should be followed thoroughly when designing and implementing each new system. Please follow as relevant:

###  Prefer data-driven design

#### Enums vs. Static Strings & Resources
Here is a thorough breakdown of Enums vs. Static `StringName` Keys vs. Resources, including general recommendations and when to use each one.

---

<b>The Grand Breakdown: Three Tools for Three Jobs</b>

First, it's crucial to understand that these are not three competing options for the same problem. They are three distinct tools that solve different kinds of problems, often working together.

*   An **Enum** is a **compile-time constant**. It represents a small, fixed set of options.
*   A **Static `StringName` Key** is a **safe, decoupled reference**. It acts as a universal "name tag" to look up data.
*   A **Resource** is the **data itself**. It is a self-contained asset that defines a game concept.

Here is a direct comparison of their architectural properties:

| Feature | `enum` | `static class` of `StringName` Keys | `Resource` (`.tres` file) |
| :--- | :--- | :--- | :--- |
| **Core Identity** | An integer with a human-readable name. | A unique, hashed string reference. | A data container asset with a unique ID (UID). |
| **Primary Purpose** | Defining a small, fixed set of states or types that rarely change. | Providing a safe, compile-time checked key to access data from a collection. | Defining a piece of game content or a concept that has its own data and identity. |
| **Compile-Time Safety**| **Excellent.** A typo is a compile error. The set of options is strictly enforced. | **Excellent.** A typo in the key class is a compile error. Provides autocompletion. | **Good.** When used as an `[Export]`, you can only assign a valid resource of that type. |
| **Data Stability (Serialization)** | **Poor/Brittle.** Saved as an integer. Reordering or removing enum members will silently corrupt saved data in scenes and resources. | **Excellent.** The key is saved as a string. It is immune to reordering in the code and is highly stable. | **Excellent.** Saved by its unique resource path and UID. It is extremely stable and resilient to code changes. |
| **Extensibility**| **Very Poor.** Requires a programmer to edit the code and recompile for every single addition. Creates a programmer bottleneck. | **Good.** Requires a programmer to add a key, but the system using the key is decoupled and doesn't need to change. | **Excellent.** A designer can create new `.tres` files at any time without any code changes. This is the most flexible option. |
| **Data Association** | **None.** An enum is just a value. You cannot attach data to `PlayerState.Walking`. You must write code elsewhere to handle what "Walking" means. | **None.** A key is just a name. It is a pointer *to* data, but it doesn't hold the data itself. | **Its Entire Purpose.** This is its superpower. A `Category.tres` can hold its own name, color, relationships to other categories, etc. |
| **Coupling**| **Very High.** Any system that uses the enum is now tightly coupled to its definition. This makes systems less reusable. | **Very Low.** The key class is a convenience for the caller. The receiver (like a Blackboard) is completely decoupled. | **Low.** Systems reference the resource, but the resource's internal data can change freely without breaking the systems that use it. |
| **Typical Workflow** | Programmer defines the enum in C#. Other programmers and designers select it from a dropdown in the editor. | Programmer defines a key in a central static class. Programmers use this key to get/set data. | Designer right-clicks in the FileSystem dock, creates a new Resource, and configures its properties in the Inspector. |

---

<b>General Recommendations & When to Use Each One</b>

1. When to Use an `enum`

**Mantra:** "Use me for a small, fixed set of **options or states** that will almost never change."

Enums are for programming logic, not for game content. They are perfect for describing *how* something is, not *what* it is.

✅ **Use an `enum` for:**

*   **Finite State Machines:** `PlayerState { Idle, Walking, Jumping, Attacking }`
*   **Core Game States:** `GameState { MainMenu, Playing, Paused, GameOver }`
*   **Fixed Options:** `QualitySetting { Low, Medium, High }`, `Direction { North, South, East, West }`
*   **Simple Damage Types (if they have no unique data):** `DamageType { Physical, Fire, Ice }`. The moment you want "Fire" to have a default "Burn" status effect associated with it, you should upgrade it to a Resource.

❌ **DO NOT use an `enum` for:**

*   **Lists of Content:** `AllItems { HealthPotion, Sword, Shield }` or `AllEnemies { Goblin, Orc, Dragon }`. This is the #1 anti-pattern. This data is content, not a state, and it will kill your scalability.

2. When to Use a `static class` of `StringName` Keys

**Mantra:** "Use me as a **safe name tag** to ask for something from a central collection."

Static keys are the glue. They are the safe, universal language your game systems use to talk to each other and ask for data without being directly wired together. They are the solution to the "magic string" problem.

✅ **Use Static `StringName` Keys for:**

*   **Blackboard Keys:** `BB.CurrentTarget`, `BB.Squad.AverageHealth`. (As we designed).
*   **Registry Keys:** `C.Enemy`, `C.PlayerFaction`. (As we designed).
*   **Event/Message Bus Names:** `Events.PlayerDied`, `Events.QuestCompleted`.
*   **Service Locator Keys:** `Services.AudioManager`, `Services.SceneLoader`.
*   **Input Action Map Names:** `InputActions.Jump`, `InputActions.Interact`.

❌ **DO NOT use Static Keys for:**

*   **Representing a State:** A state machine should use an `enum` for clarity and type-safety within its own logic. `currentState = PlayerState.Jumping;` is better than `currentState = "Jumping";`.
*   **Holding Data:** The key is just the name tag. The data it points to should be in the collection (like a Blackboard) or be a Resource.

3. When to Use a `Resource` (`.tres` file)

**Mantra:** "Use me to define any piece of **game content or a concept** that needs its own unique data."

Resources are your game's database. They are the nouns of your world. If a designer needs to create, view, edit, or balance it in the Godot Inspector, it should be a Resource.

✅ **Use a `Resource` for:**

*   **Item Definitions:** `HealthPotion.tres` (with data for healing amount, icon, description).
*   **Enemy Archetypes:** `Goblin.tres` (with data for health, speed, model scene, loot table).
*   **Character Classes:** `Warrior.tres` (with data for starting stats, abilities).
*   **Semantic Tags:** Your `Category.tres` system is a perfect example. The "Enemy" category is a concept with its own data.
*   **Quests, Abilities, Status Effects, Dialogue Trees, Loot Tables...** anything that is fundamentally game *content*.

❌ **DO NOT use a `Resource` for:**

*   **Simple States:** It's massive overkill to create `IdleState.tres` when an enum works perfectly.
*   **As a Key:** The resource is the *value* you get from a dictionary. The *key* should be a `StringName`.

<b>Putting It All Together: The Cookbook Analogy</b>

Imagine your entire game architecture is a giant cookbook.

*   The **`Enums`** are the fixed chapter titles printed in the book: `Appetizers`, `Main Courses`, `Desserts`. They are a small, fixed set that organizes the content.

*   The **`Static StringName` Keys** are the names of the recipes in the index: `"Grandma's Lasagna"`, `"Spicy Chili"`, `"Chocolate Cake"`. They are safe, unique references you use to look up a specific recipe.

*   The **`Resources`** are the actual recipe cards themselves. `GrandmasLasagna.tres` is a file containing all the data: the ingredients (health, speed), the cooking time (AI behavior), the instructions (logic), and a picture of the final dish (the scene/model).

You use the **Key** ("Grandma's Lasagna") to look up the **Resource** (the recipe card) which is found in the chapter defined by the **Enum** (`Main Courses`). They all work together to create a powerful, organized, and scalable system.

#### Breakdown of Disadvantages of Enums

The advantage of using a static class of StringNames over an enum is not about the workflow for adding a key; it's about what that key enables and how it couples your systems together.
Here is a breakdown of the key differences.

1. The Core Difference: Coupling
This is the most important architectural reason.
Enum: An enum creates a hard, compile-time dependency. If your Blackboard class has a method like SetVar(BBDataSig key, Variant value), that Blackboard is now forever tied to the BBDataSig enum. You cannot use it with any other type of key. It forces every system that wants to talk to the blackboard to also know about and use the BBDataSig enum.
Static String Keys: The Blackboard class method is SetVar(StringName key, Variant value). The Blackboard knows nothing about your BlackboardKeys static class. It is completely decoupled. The static BlackboardKeys class is a convenience and safety tool for the caller, not a requirement for the receiver.
Analogy:
An enum is like a custom-shaped plug and socket. The Blackboard is the socket, and it will only accept the BBDataSig plug.
A StringName is like a USB-C port. The Blackboard just has a standard USB-C port (StringName). The BlackboardKeys class is a certified, safe cable that you choose to use to plug things in. But someone else could come along with a different cable (another static keys class, or even a raw string) and it would still plug in just fine.
This decoupling means your Blackboard system is far more generic, reusable, and extensible.
2. The Nature of the Value
Enum: An enum member is just a name for an integer. BBDataSig.CurrentTarget is really just 3 (or whatever its integer value is). This value is only meaningful in the context of the BBDataSig enum itself. You can't use it as a human-readable key in a data file.
Static String Keys: The value of BlackboardKeys.CurrentTarget is the StringName "CurrentTarget". This value is self-describing. It can be used as a key in a dictionary, serialized to a text file, sent over a network, or displayed in a debugger, and it always retains its meaning.
3. Data Stability (Serialization)
This is a huge, often overlooked problem with enums.
Enum: Godot saves enums in .tscn or .tres files as their integer value. Let's say you have enum Tags { Player, Enemy } (Player=0, Enemy=1). You have a hundred scenes where you've exported this enum and set it to Enemy. One day, a programmer changes the enum to enum Tags { Scenery, Player, Enemy }. Now Player=1 and Enemy=2. You have just silently corrupted the data in all one hundred scenes. They will all now load the wrong value.
Static String Keys: When you use a string-keyed dictionary, the key "Enemy" is saved in the file as the literal string "Enemy". You can add a million other keys to your RegistryKeys.cs file; it will never, ever break the data for that existing entry. This makes your data far more robust and stable over the long term.
4. Extensibility for Other Systems
Because the StringName approach decouples the blackboard, other systems can interact with it in ways an enum would prevent. For example, you could write a generic animation event system where an animator can type a string key into the animation track (e.g., "SetBlackboardVar"), and a value, and have it directly set a variable on an entity's blackboard without the animation system needing to know anything about BBDataSig.

