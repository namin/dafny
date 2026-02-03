// RUN: %verify --allow-warnings --print-ranges "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// === STRUCTURALLY SOUND SKETCHES (should verify with only hole warnings) ===

// Example 1: Max with hole in else branch — structurally sound
method MaxWithHole(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  if x <= y {
    m := y;
  } else {
    hole "else branch";
  }
}

// Example 2: Max fully filled — should verify fully (no warnings)
method MaxFilled(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  if x <= y {
    m := y;
  } else {
    m := x;
  }
}

// Example 3: Entire body is a hole — structurally sound (trivially)
method MaxAllHole(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  hole "entire body";
}

// Example 4: Two holes — structurally sound
method MaxTwoHoles(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  if x <= y {
    hole "then branch";
  } else {
    hole "else branch";
  }
}

// Example 5: Hole in if, filled else — structurally sound
method MaxHoleOneBranch(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  if x <= y {
    hole "then branch";
  } else {
    m := x;
  }
}

// Example 6: Hole sandwiched between code — structurally sound
method HoleThenAssert(x: int) returns (r: int)
  ensures r > 0
{
  r := 0;
  hole "must make r positive";
  // With assume false, this assertion is vacuously true
}

// Example 7: Hole before a method call — structurally sound
method NeedsPositive(n: int)
  requires n > 0
{}

method HoleThenCall(x: int)
{
  var y := 0;
  hole "before call";
  // With assume false, the precondition check is vacuously true
}

// === STRUCTURAL ERRORS (should produce real errors) ===

// Example 8: Structural bug in else branch — m := y is wrong when x > y
method MaxBrokenElse(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  if x <= y {
    hole "then branch";
  } else {
    m := y; // BUG: should be x
  }
}

// Example 9: Structural bug in then branch with hole in else
method MaxBrokenThen(x: int, y: int) returns (m: int)
  ensures (m == x || m == y) && x <= m && y <= m
{
  if x <= y {
    m := x; // BUG: should be y (x <= y means x might not be max)
  } else {
    hole "else branch";
  }
}
