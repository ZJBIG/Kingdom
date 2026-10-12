using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private int styledEra = -1;
    private Image eraContentSurface, eraDetailSurface;

    public static Color GetEraAccent(TechLevel era) => era switch
    {
        TechLevel.Animal => new Color(.63f, .72f, .43f),
        TechLevel.StoneAge => new Color(.81f, .66f, .43f),
        TechLevel.Medieval => new Color(.77f, .48f, .32f),
        TechLevel.Industrial => new Color(.60f, .72f, .77f),
        TechLevel.Spacer => new Color(.37f, .73f, .82f),
        TechLevel.Ultra => new Color(.69f, .57f, .86f),
        _ => new Color(.84f, .76f, .48f)
    };

    private static void ApplyEraRowStyle(GameObject row, TechLevel era, int index)
    {
        ApplyListRowStyle(row, index);
        Color accent = GetEraAccent(era);
        Image surface = row.GetComponent<Image>();
        surface.color = new Color(accent.r, accent.g, accent.b, index % 2 == 0 ? .12f : .18f);
        Transform stripe = row.transform.Find("EraAccent");
        if (stripe != null) stripe.GetComponent<Image>().color = accent;
    }

    private static void SetRowMetadata(GameObject row, string value)
    {
        Transform metadata = row.transform.Find("Metadata");
        if (metadata != null) metadata.GetComponent<TMP_Text>().text = value ?? string.Empty;
    }

    private void RefreshEraSurfaces()
    {
        GameManager game = GameManager.Instance;
        if (game == null || styledEra == (int)game.State.TechLevel) return;
        styledEra = (int)game.State.TechLevel;
        if (eraContentSurface == null)
            eraContentSurface = transform.Find("SafeAreaRoot/Content/Surface")?.GetComponent<Image>();
        if (eraDetailSurface == null)
            eraDetailSurface = transform.Find("SafeAreaRoot/DetailPanel/Surface")?.GetComponent<Image>();
        Color accent = GetEraAccent(game.State.TechLevel);
        Color tint = Color.Lerp(new Color(.68f, .68f, .68f, .96f), accent, .22f);
        tint.a = .96f;
        if (eraContentSurface != null) eraContentSurface.color = tint;
        if (eraDetailSurface != null) eraDetailSurface.color = tint;
    }
}
