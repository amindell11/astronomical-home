- RL pilot only (firing controls are handled by controller, pilot tries to get in a gun snap (within angle tolerance of facing enemy))
- SAC instead of PPO for piloting
- hierarchical RL (decision maker over programmed behaviors, gradually replace these behaviors with Low level RL agents)
- constant reward signal safe position (velocity vector does not project hitting asteroid in next few seconds) and enemy facing
- BC to learn weapon timings


Rrelative position rewards the agent for positioning itself behind the opponent with its nose pointing at the
opponent. It also penalizes the agent when the opposite
situation occurs.
• Rtrack θ penalizes the agent for having a non-zero track
angle (angle between ownship aircraft nose and the
center of the opponent aircraft), regardless of its position
relative to the opponent.
• Rclosure rewards the agent for getting closer to the
opponent when pursuing and penalizes it for getting
closer when being pursued.
• Rgunsnap(blue)
is a reward given when the agent
achieves a minimum track angle and is within a particular range of distances, similar to WEZ damage in
the environment.
• Rgunsnap(red)
is a penalty given when the opponent
achieves a minimum track angle and is within a par-
ticular range of distances, similar to WEZ damage in
the environment.
• Rdeck penalizes the agent for flying below a minimum
altitude threshold.
• Rtoo close penalizes the agent for violating a minimum
distance threshold within a range of adverse angles
(angle between the opponent’s tail and the center of the
ownship aircraft) and is meant to discourage overshooting when pursuing.

https://arxiv.org/pdf/2105.00990