// RUN: %verify --allow-warnings --print-ranges "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Example 1: Max with hole in else branch
// The hole doesn't set m, so the postcondition may not hold — reported as obligation
method MaxWithHole(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  if x <= y {
    m := y;
  } else {
    hole "else branch";
    m := 0; // wrong filler — obligation should fire
  }
}

// Example 2: Max fully filled - should verify fully (no errors)
method MaxFilled(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  if x <= y {
    m := y;
  } else {
    m := x;
  }
}

// Example 3: Entire body is a hole
method MaxAllHole(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  m := 0; // default to satisfy definite-assignment
  hole "entire body";
}

// Example 4: Two holes — obligations from both branches
method MaxTwoHoles(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  m := 0; // default to satisfy definite-assignment
  if x <= y {
    hole "then branch";
  } else {
    hole "else branch";
  }
}

// Example 5: Hole in if, filled else — only one branch has obligation
method MaxHoleOneBranch(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  m := 0; // default to satisfy definite-assignment
  if x <= y {
    hole "then branch";
  } else {
    m := x;
  }
}
