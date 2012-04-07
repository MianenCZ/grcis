﻿using System;
using System.Collections.Generic;
using OpenTK;

// Interfaces and objects for ray-based rendering.
namespace Rendering
{
  /// <summary>
  /// Set operations for CSG inner nodes.
  /// </summary>
  public enum SetOperation
  {
    Union,
    Intersection,
    Difference,
    Xor,
  }

  /// <summary>
  /// Builtin
  /// </summary>
  public class PropertyName
  {
    /// <summary>
    /// Surface property = base color.
    /// </summary>
    public static string COLOR = "color";

    /// <summary>
    /// Surface property = texture (multi-attribute).
    /// </summary>
    public static string TEXTURE = "texture";

    /// <summary>
    /// (Perhaps) globally used reflectance model.
    /// </summary>
    public static string REFLECTANCE_MODEL = "reflectance";

    /// <summary>
    /// Surface property: material description. Must match used reflectance model.
    /// </summary>
    public static string MATERIAL = "material";
  }

  /// <summary>
  /// General scene node (hierarchical 3D scene used in ray-based rendering).
  /// </summary>
  public interface ISceneNode : IIntersectable
  {
    /// <summary>
    /// Reference to a parent node (null for root node).
    /// </summary>
    ISceneNode Parent
    {
      get;
      set;
    }

    /// <summary>
    /// Transform from this space to parent's one.
    /// </summary>
    Matrix4d ToParent
    {
      get;
      set;
    }

    /// <summary>
    /// Transform from parent space to this node's space.
    /// </summary>
    Matrix4d FromParent
    {
      get;
      set;
    }

    /// <summary>
    /// Collection of node's children, can be null.
    /// </summary>
    ICollection<ISceneNode> Children
    {
      get;
      set;
    }

    /// <summary>
    /// True for object root (subject to animation). 3D texture coordinates should use Object space.
    /// </summary>
    bool ObjectRoot
    {
      get;
      set;
    }

    /// <summary>
    /// Retrieves value of the given Attribute. Looks in parent nodes if not found locally.
    /// </summary>
    /// <param name="name">Attribute name.</param>
    /// <returns>Attribute value or null if not found.</returns>
    object GetAttribute ( string name );

    /// <summary>
    /// Retrieves value of the given Attribute. Looks only in this node.
    /// </summary>
    /// <param name="name">Attribute name.</param>
    /// <returns>Attribute value or null if not found.</returns>
    object GetLocalAttribute ( string name );

    /// <summary>
    /// Sets the new value of the given attribute.
    /// </summary>
    /// <param name="name">Attribute name.</param>
    /// <param name="value">Attribute value.</param>
    void SetAttribute ( string name, object value );

    /// <summary>
    /// Returns transform from the Local space (Solid) to the World space.
    /// </summary>
    /// <returns>Transform matrix.</returns>
    Matrix4d ToWorld ();

    /// <summary>
    /// Returns transform from the Local space (Solid) to the Object space (subject to animation).
    /// </summary>
    /// <returns>Transform matrix.</returns>
    Matrix4d ToObject ();

    /// <summary>
    /// Collects texture sequence in the right (application) order.
    /// </summary>
    /// <returns>Sequence of textures or null.</returns>
    LinkedList<ITexture> GetTextures ();
  }

  /// <summary>
  /// Elementary solid - atomic building block of a scene.
  /// </summary>
  public interface ISolid : ISceneNode
  {
  }

  /// <summary>
  /// Common code for ISceneNode.
  /// </summary>
  public abstract class DefaultSceneNode : ISceneNode
  {
    protected LinkedList<ISceneNode> children;

    public ICollection<ISceneNode> Children
    {
      get
      {
        return children;
      }
      set
      {
        children.Clear();
        foreach ( ISceneNode sn in value )
          children.AddLast( sn );
      }
    }

    public ISceneNode Parent
    {
      get;
      set;
    }

    public Matrix4d ToParent
    {
      get;
      set;
    }

    public Matrix4d FromParent
    {
      get;
      set;
    }

    public bool ObjectRoot
    {
      get;
      set;
    }

    protected Dictionary<string, object> attributes;

    public object GetAttribute ( string name )
    {
      if ( attributes != null )
      {
        object result;
        if ( attributes.TryGetValue( name, out result ) )
          return result;
      }
      if ( Parent != null ) return Parent.GetAttribute( name );
      return null;
    }

    public object GetLocalAttribute ( string name )
    {
      object result;
      if ( attributes == null ||
           !attributes.TryGetValue( name, out result ) )
        return null;

      return result;
    }

    public void SetAttribute ( string name, object value )
    {
      if ( attributes == null )
        attributes = new Dictionary< string, object >();
      attributes[ name ] = value;
    }

    /// <summary>
    /// Inserts one new child node to this parent node.
    /// </summary>
    /// <param name="ch">Child node to add.</param>
    /// <param name="toParent">Transform from local space of the child to the parent's space.</param>
    public virtual void InsertChild ( ISceneNode ch, Matrix4d toParent )
    {
      children.AddLast( ch );
      ch.ToParent = toParent;
      toParent.Invert();
      ch.FromParent = toParent;
      ch.Parent = this;
    }

    /// <summary>
    /// Returns transform from the Local space (Solid) to the World space.
    /// </summary>
    /// <returns>Transform matrix.</returns>
    public Matrix4d ToWorld ()
    {
      if ( Parent == null ) return Matrix4d.Identity;
      return( ToParent * Parent.ToWorld() );
    }

    /// <summary>
    /// Returns transform from the Local space (Solid) to the Object space (subject to animation).
    /// </summary>
    /// <returns>Transform matrix.</returns>
    public Matrix4d ToObject ()
    {
      if ( ObjectRoot || Parent == null ) return Matrix4d.Identity;
      return( ToParent * Parent.ToObject() );
    }

    /// <summary>
    /// Collects texture sequence in the right (application) order.
    /// </summary>
    /// <returns>Sequence of textures or null.</returns>
    public LinkedList<ITexture> GetTextures ()
    {
      LinkedList<ITexture> result = null;
      if ( Parent != null )
        result = Parent.GetTextures();

      object local = GetLocalAttribute( PropertyName.TEXTURE );
      if ( local == null ) return result;

      if ( local is ITexture )
      {
        if ( result == null )
          result = new LinkedList<ITexture>();
        result.AddLast( (ITexture)local );
      }
      else
        if ( local is IEnumerable<ITexture> )
          if ( result == null )
            result = new LinkedList<ITexture>( (IEnumerable<ITexture>)local );
          else
            foreach ( ITexture tex in (IEnumerable<ITexture>)local )
              result.AddLast( tex );

      return result;
    }

    /// <summary>
    /// Computes the complete intersection of the given ray with the object.
    /// </summary>
    /// <param name="p0">Ray origin.</param>
    /// <param name="p1">Ray direction vector.</param>
    /// <returns>Sorted list of intersection records.</returns>
    public virtual LinkedList<Intersection> Intersect ( Vector3d p0, Vector3d p1 )
    {
      if ( children == null || children.Count == 0 )
        return null;

      ISceneNode child = children.First.Value;
      Vector3d origin = Vector3d.TransformPosition( p0, child.FromParent );
      Vector3d dir    = Vector3d.TransformVector(   p1, child.FromParent );
      // ray in local child's coords: [ origin, dir ]

      return child.Intersect( origin, dir );
    }

    /// <summary>
    /// Complete all relevant items in the given Intersection object.
    /// </summary>
    /// <param name="inter">Intersection instance to complete.</param>
    public virtual void CompleteIntersection ( Intersection inter )
    { }

    public DefaultSceneNode ()
    {
      children = new LinkedList<ISceneNode>();
      attributes = null;
      ObjectRoot = false;
    }
  }

  /// <summary>
  /// CSG set operations in a inner scene node..
  /// </summary>
  public class CSGInnerNode : DefaultSceneNode
  {
    /// <summary>
    /// Delegate function for boolean operations
    /// </summary>
    delegate bool BooleanOperation ( bool x, bool y );

    /// <summary>
    /// Current boolean operation.
    /// </summary>
    BooleanOperation bop;

    public CSGInnerNode ( SetOperation op )
    {
      switch ( op )
      {
        case SetOperation.Intersection:
          bop = ( x, y ) => x && y;
          break;
        case SetOperation.Difference:
          bop = ( x, y ) => x && !y;
          break;
        case SetOperation.Xor:
          bop = ( x, y ) => x ^ y;
          break;
        case SetOperation.Union:
        default:
          bop = ( x, y ) => x || y;
          break;
      }
    }

    /// <summary>
    /// Computes the complete intersection of the given ray with the object.
    /// </summary>
    /// <param name="p0">Ray origin.</param>
    /// <param name="p1">Ray direction vector.</param>
    /// <returns>Sorted list of intersection records.</returns>
    public override LinkedList<Intersection> Intersect ( Vector3d p0, Vector3d p1 )
    {
      if ( children == null || children.Count == 0 )
        return null;

      LinkedList<Intersection> result = null;

      bool leftOp = true;  // the 1st pass => left operand
      foreach ( ISceneNode child in children )
      {
        Vector3d origin = Vector3d.TransformPosition( p0, child.FromParent );
        Vector3d dir    = Vector3d.TransformVector(   p1, child.FromParent );
        // ray in local child's coords: [ origin, dir ]

        LinkedList<Intersection> partial = child.Intersect( origin, dir );
        if ( partial == null )
          partial = new LinkedList<Intersection>();

        if ( leftOp )
        {
          leftOp = false;
          result = partial;
          continue;
        }

        // resolve one binary operation (result := left # partial):
        bool insideLeft  = false;
        bool insideRight = false;
        bool insideResult = bop( false, false );

        LinkedList<Intersection> left = result;
        // result .. empty so far
        result = new LinkedList<Intersection>();

        double lowestT = Double.NegativeInfinity;
        Intersection leftFirst  = (left.First    == null) ? null : left.First.Value;
        Intersection rightFirst = (partial.First == null) ? null : partial.First.Value;
        bool minLeft  = (leftFirst  != null && leftFirst.T  == lowestT);
        bool minRight = (rightFirst != null && rightFirst.T == lowestT);

        if ( insideResult && !minLeft && !minRight )    // we need to insert negative infinity..
        {
          Intersection n = new Intersection( null );
          n.T = Double.NegativeInfinity;
          n.Enter = true;
          result.AddLast( n );
          insideResult = true;
        }

        while ( leftFirst != null || rightFirst != null )
        {
          double leftVal =  (leftFirst  != null) ? leftFirst.T  : double.PositiveInfinity;
          double rightVal = (rightFirst != null) ? rightFirst.T : double.PositiveInfinity;
          lowestT = Math.Min( leftVal, rightVal );
          minLeft  = leftVal  == lowestT;
          minRight = rightVal == lowestT;

          Intersection first = null;
          if ( minRight )
          {
            first = rightFirst;
            partial.RemoveFirst();
            rightFirst = (partial.First == null) ? null : partial.First.Value;
            insideRight = !insideRight;
          }
          if ( minLeft )
          {
            first = leftFirst;
            left.RemoveFirst();
            leftFirst = (left.First == null) ? null : left.First.Value;
            insideLeft = !insideLeft;
          }
          bool newResult = bop( insideLeft, insideRight );

          if ( newResult != insideResult )
          {
            first.Enter = insideResult = newResult;
            result.AddLast( first );
          }
        }
      }

      return result;
    }
  }

  /// <summary>
  /// Default scene class for ray-based rendering.
  /// </summary>
  public abstract class DefaultRayScene : IRayScene
  {
    /// <summary>
    /// Scene model (whatever is able to compute ray intersections).
    /// </summary>
    public IIntersectable Intersectable
    {
      get;
      set;
    }

    /// <summary>
    /// Background color.
    /// </summary>
    public double[] BackgroundColor
    {
      get;
      set;
    }

    /// <summary>
    /// Camera = primary ray generator.
    /// </summary>
    public ICamera Camera
    {
      get;
      set;
    }

    /// <summary>
    /// Set of light sources.
    /// </summary>
    public ICollection<ILightSource> Sources
    {
      get;
      set;
    }
  }
}
