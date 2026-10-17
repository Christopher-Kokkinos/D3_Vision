namespace D3Vision.Core;

/// <summary>Guardian actor identities from the supported build's SNO metadata; excludes minions, effects and UI actors.</summary>
public static class RiftGuardianIdentity
{
    public const int TrackingRadius = 120;

    public static bool IsGuardian(int actorSno) =>
        actorSno
            is 343743
                or 343751
                or 343759
                or 343767
                or 344119
                or 344389
                or 345004
                or 346563
                or 353517
                or 353535
                or 353823
                or 353874
                or 354050
                or 354144
                or 354652
                or 357917
                or 358208
                or 358429
                or 358489
                or 358614
                or 359094
                or 359688
                or 360281
                or 360636
                or 426943
                or 472772
                or 487291
                or 487293
                or 487294
                or 487295
                or 487296
                or 487297
                or 487298
                or 487299
                or 487300
                or 487301
                or 487302
                or 488941
                or 488942
                or 488943
                or 488944
                or 488945
                or 488946
                or 488947
                or 488948
                or 488949
                or 488950
                or 488951;
}
