# Suika Billiards

A VRChat hybrid Suika-Billiards table by nFinity8!

This table is forked off the MS-VRCSA Billiards table by Neko Mabel and Sacchan, which is itself a fork of the Pool Parlor table by metaphira and Toasterly, originally coded by Harry-T.

## Game Modes

I'm implementing 2 categories of gameplay, with WIP rulesets.

Common features include replacing pocketing balls with "merging" 2 balls of the same number (or "fruit").

### Suika-12

This ruleset takes the gameplay of 9 ball but uses Suika physics mechanics.

The rack starts off with 1 of each 11 balls, plus a second cherry(1).

On your turn, try to hit the cue ball into either of the two lowest balls, knocking it into the other one, thus merging them into the next ball up the chain. The player who merges the two watermelons/suika/11 at the end wins!

Some rule considerations:
- Knocking an object ball off the table soft-locks the game if not replaced. Should this be an instant loss (like having a fruit escape the bucket in Suika Game) or should it just be a scratch and have the next player place it back on the table ball-in-hand?
- How should the initial rack and break work?

### Suika Pool

This ruleset more closely implements the Suika Game using billiards mechanics.

The table starts off with a single "money ball" on the table - a watermelon/suika/11 ball. On your turn, you are given a random ball 1-5 to place anywhere at the foot of the table. Then, hit the ball directly. A valid break simply must hit the money ball.

Players' balls are separate suites (opponents' balls appear grayed out).

The player who merges a watermelon/suika/11 with the money ball wins.

Some rule considerations:
- What happens when you knock a ball off the table? (Can be different for type: your object ball, your opponent's object ball, the money ball)
- Is making two watermelons on your own and merging them a win, or do you need to use the starting watermelon? (Granted the former method would probably take a lot longer)
- Always having ball-in-hand makes a foul inconsequential (versus normal turn ending due to failure to merge a ball). But there are fewer ways to foul since there's no cue ball.

## Usage  
  
### Setup:  
Import the package  
Click MS-VRCSA->Set Up Pool Table Layers  
	- this names layer 22 to 'BilliardsModule' and sets the collision matrix so that it only collides with itself  
Place one or more Prefab/MS-VRCA Table prefabs into your scene  
You can freely add/remove tables from the tables list under the hierarchy at BilliardsModule/intl.table/  
	- The table prefabs are in the folder Modules/BilliardsModule/Prefabs  
Because tables can be swapped out they can't easily be lightmapped. Use just one table if you wish to have light mapping  
	- Remove the unused tables from the hierarchy under BilliardsModule/intl.table/  
Alternatively if using VRC Light Volumes by RED_SIM, make sure to change the table surface shader to the _VRCLV Variant.  
  
### Table Creation  
Duplicate an existing table prefab and replace the mesh ('table' object) with your own.  
Place the table prefab on its own in to the scene and select and enable gizmos it to display its physical setup, adjust ModelData settings to match your model.  
Copy the hierarchy of the existing tables and you should be fine, objects whose name begin with a period are used in the code, so don't change their names.  
The shader for the table's Metallic/Smoothness texture can be exported as a 2 channel png with photoshop's Export As and ticking the '[x]Smaller Filer (8-bit)' option  
  
## Credits  
Neko Mabel:  
- Ball-cushion collision function, ball-to-ball collision, rolling, and bounce functions  
- Deep knowledge about all things billiards related  
  
Sacchan:  
- Everything else in the MS-VRCSA Billiards table

nFinity8:
- Suika gameplay, ball-merging physics
