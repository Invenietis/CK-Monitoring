using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Monitoring;

/// <summary>
/// Captures an atomic change of an <see cref="IdentityCard"/>.
/// </summary>
public sealed class IdentiCardChangedEvent
{
    string? _packedAddedInfo;
    string? _packedIdentities;

    /// <summary>
    /// Initializes a new <see cref="IdentiCardChangedEvent"/>.
    /// </summary>
    /// <param name="c">Source of the change.</param>
    /// <param name="added">The keys and values added. Must not be empty.</param>
    /// <param name="identities">The modified identities.</param>
    public IdentiCardChangedEvent( IdentityCard c, IReadOnlyList<(string Key, string Value)> added, IReadOnlyDictionary<string, IReadOnlyCollection<string>> identities )
    {
        IdentityCard = c;
        AddedInfo = added;
        Identities = identities;
    }

    /// <summary>
    /// Gets the identity card that changed.
    /// </summary>
    public IdentityCard IdentityCard { get; }

    /// <summary>
    /// Gets a never empty added list of keys and values added.
    /// </summary>
    public IReadOnlyList<(string Key, string Value)> AddedInfo { get; }

    /// <summary>
    /// Gets the new identities.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Identities { get; }

    /// <summary>
    /// Gets the <see cref="AddedInfo"/> as packed string (see <see cref="IdentityCard.Pack(IReadOnlyList{ValueTuple{string, string}})"/>
    /// </summary>
    public string PackedAddedInfo => _packedAddedInfo ??= IdentityCard.Pack( AddedInfo );

    /// <summary>
    /// Gets the <see cref="Identities"/> as a packed string (see <see cref="IdentityCard.Pack(IReadOnlyDictionary{string, IReadOnlyCollection{string}})"/>).
    /// </summary>
    public string PackedIdentities => _packedIdentities ??= IdentityCard.Pack( Identities );

}
