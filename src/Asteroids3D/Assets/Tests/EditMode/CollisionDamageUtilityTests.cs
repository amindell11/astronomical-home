using Damage;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for CollisionDamageUtility static methods.
/// Tests pure mathematical calculations without Unity scene dependencies.
/// </summary>
public class CollisionDamageUtilityTests
{
    #region KineticEnergy Tests

    [Test]
    public void KineticEnergy_ZeroVelocity_ReturnsZero()
    {
        // Arrange
        float mass = 10f;
        Vector3 velocity = Vector3.zero;

        // Act
        float result = CollisionDamageUtility.KineticEnergy(mass, velocity);

        // Assert
        Assert.AreEqual(0f, result, 0.001f, "Kinetic energy should be zero when velocity is zero");
    }

    [Test]
    public void KineticEnergy_ZeroMass_ReturnsZero()
    {
        // Arrange
        float mass = 0f;
        Vector3 velocity = new Vector3(10f, 5f, 0f);

        // Act
        float result = CollisionDamageUtility.KineticEnergy(mass, velocity);

        // Assert
        Assert.AreEqual(0f, result, 0.001f, "Kinetic energy should be zero when mass is zero");
    }

    [Test]
    public void KineticEnergy_ClosedFormVsAnalytical_MatchesExpected()
    {
        // Arrange
        float mass = 5f;
        Vector3 velocity = new Vector3(3f, 4f, 0f); // magnitude = 5
        float expectedEnergy = 0.5f * mass * (velocity.sqrMagnitude); // ½mv² = ½ * 5 * 25 = 62.5

        // Act
        float result = CollisionDamageUtility.KineticEnergy(mass, velocity);

        // Assert
        Assert.AreEqual(expectedEnergy, result, 0.001f, 
            "KineticEnergy closed-form calculation should match analytical ½mv²");
    }

    [Test]
    public void KineticEnergy_AnalyticalFormula_MatchesManualCalculation()
    {
        // Arrange
        float mass = 2.5f;
        Vector3 velocity = new Vector3(6f, 8f, 0f); // |v| = 10, |v|² = 100
        float manualCalculation = 0.5f * mass * 100f; // ½ * 2.5 * 100 = 125

        // Act
        float result = CollisionDamageUtility.KineticEnergy(mass, velocity);

        // Assert
        Assert.AreEqual(manualCalculation, result, 0.001f,
            "Result should match manual analytical calculation");
    }

    [Test]
    public void KineticEnergy_3DVelocity_UsesCorrectMagnitude()
    {
        // Arrange
        float mass = 1f;
        Vector3 velocity = new Vector3(1f, 2f, 2f); // |v|² = 1 + 4 + 4 = 9
        float expected = 0.5f * mass * 9f; // 4.5

        // Act
        float result = CollisionDamageUtility.KineticEnergy(mass, velocity);

        // Assert
        Assert.AreEqual(expected, result, 0.001f,
            "Should correctly handle 3D velocity vectors");
    }

    #endregion

    #region RelativeKineticEnergy Tests

    [Test]
    public void RelativeKineticEnergy_IdenticalObjects_ReturnsZero()
    {
        // Arrange
        float massA = 10f, massB = 10f;
        Vector3 velocityA = new Vector3(5f, 0f, 0f);
        Vector3 velocityB = new Vector3(5f, 0f, 0f); // Same velocity

        // Act
        float result = CollisionDamageUtility.RelativeKineticEnergy(massA, velocityA, massB, velocityB);

        // Assert
        Assert.AreEqual(0f, result, 0.001f,
            "Relative kinetic energy should be zero when objects have identical velocities");
    }

    [Test]
    public void RelativeKineticEnergy_Symmetry_ABAEqualsBAA()
    {
        // Arrange
        float massA = 3f, massB = 7f;
        Vector3 velocityA = new Vector3(10f, 0f, 0f);
        Vector3 velocityB = new Vector3(2f, 4f, 0f);

        // Act
        float resultAB = CollisionDamageUtility.RelativeKineticEnergy(massA, velocityA, massB, velocityB);
        float resultBA = CollisionDamageUtility.RelativeKineticEnergy(massB, velocityB, massA, velocityA);

        // Assert
        Assert.AreEqual(resultAB, resultBA, 0.001f,
            "RelativeKineticEnergy should be symmetric: f(A,B) = f(B,A)");
    }

    [Test]
    public void RelativeKineticEnergy_ReducedMassFormula_MatchesExpected()
    {
        // Arrange
        float massA = 4f, massB = 6f;
        Vector3 velocityA = new Vector3(8f, 0f, 0f);
        Vector3 velocityB = new Vector3(2f, 0f, 0f);
        
        // Manual calculation
        Vector3 relativeVelocity = velocityA - velocityB; // (6, 0, 0)
        float reducedMass = (massA * massB) / (massA + massB); // (4*6)/(4+6) = 24/10 = 2.4
        float expected = 0.5f * reducedMass * relativeVelocity.sqrMagnitude; // 0.5 * 2.4 * 36 = 43.2

        // Act
        float result = CollisionDamageUtility.RelativeKineticEnergy(massA, velocityA, massB, velocityB);

        // Assert
        Assert.AreEqual(expected, result, 0.001f,
            "Should correctly compute reduced mass formula μ = (m₁m₂)/(m₁+m₂)");
    }

    [Test]
    public void RelativeKineticEnergy_ZeroMass_ReturnsZero()
    {
        // Arrange
        float massA = 0f, massB = 10f;
        Vector3 velocityA = new Vector3(5f, 0f, 0f);
        Vector3 velocityB = new Vector3(1f, 0f, 0f);

        // Act
        float result = CollisionDamageUtility.RelativeKineticEnergy(massA, velocityA, massB, velocityB);

        // Assert
        Assert.AreEqual(0f, result, 0.001f,
            "Relative kinetic energy should be zero when either mass is zero");
    }

    [Test]
    public void RelativeKineticEnergy_OppositeVelocities_MaximumRelativeSpeed()
    {
        // Arrange
        float massA = 2f, massB = 3f;
        Vector3 velocityA = new Vector3(5f, 0f, 0f);
        Vector3 velocityB = new Vector3(-5f, 0f, 0f); // Opposite direction
        
        // Expected: relative velocity = (10, 0, 0), |v_rel|² = 100
        float reducedMass = (2f * 3f) / (2f + 3f); // 6/5 = 1.2
        float expected = 0.5f * reducedMass * 100f; // 0.5 * 1.2 * 100 = 60

        // Act
        float result = CollisionDamageUtility.RelativeKineticEnergy(massA, velocityA, massB, velocityB);

        // Assert
        Assert.AreEqual(expected, result, 0.001f,
            "Opposite velocities should produce maximum relative kinetic energy");
    }

    #endregion

    #region ComputeDamage Tests

    [Test]
    public void ComputeDamage_SingleBody_ScalesKineticEnergyCorrectly()
    {
        // Arrange
        float mass = 5f;
        Vector3 velocity = new Vector3(0f, 10f, 0f); // |v|² = 100
        float scale = 0.01f;
        float expectedKE = 0.5f * mass * 100f; // 250
        float expectedDamage = expectedKE * scale; // 2.5

        // Act
        float result = CollisionDamageUtility.ComputeDamage(mass, velocity, scale);

        // Assert
        Assert.AreEqual(expectedDamage, result, 0.001f,
            "ComputeDamage should scale kinetic energy by the provided factor");
    }

    [Test]
    public void ComputeDamage_TwoBodies_UsesRelativeKineticEnergy()
    {
        // Arrange
        float massA = 2f, massB = 8f;
        Vector3 velocityA = new Vector3(6f, 0f, 0f);
        Vector3 velocityB = new Vector3(2f, 0f, 0f);
        float scale = 0.05f;

        float expectedRelativeKE = CollisionDamageUtility.RelativeKineticEnergy(massA, velocityA, massB, velocityB);
        float expectedDamage = expectedRelativeKE * scale;

        // Act
        float result = CollisionDamageUtility.ComputeDamage(massA, velocityA, massB, velocityB, scale);

        // Assert
        Assert.AreEqual(expectedDamage, result, 0.001f,
            "Two-body ComputeDamage should use RelativeKineticEnergy");
    }

    #endregion

    #region Edge Cases

    [Test]
    public void RelativeKineticEnergy_NegativeMass_ReturnsZero()
    {
        // Arrange
        float massA = -1f, massB = 5f;
        Vector3 velocityA = Vector3.one;
        Vector3 velocityB = Vector3.zero;

        // Act
        float result = CollisionDamageUtility.RelativeKineticEnergy(massA, velocityA, massB, velocityB);

        // Assert
        Assert.AreEqual(0f, result, 0.001f,
            "Should handle negative mass gracefully by returning zero");
    }

    [Test]
    public void KineticEnergy_LargeNumbers_HandlesCorrectly()
    {
        // Arrange
        float mass = 1000f;
        Vector3 velocity = new Vector3(100f, 100f, 100f); // |v|² = 30000
        float expected = 0.5f * mass * 30000f; // 15,000,000

        // Act
        float result = CollisionDamageUtility.KineticEnergy(mass, velocity);

        // Assert
        Assert.AreEqual(expected, result, 1f, // Allow 1 unit tolerance for large numbers
            "Should handle large kinetic energy calculations");
    }

    #endregion
} 