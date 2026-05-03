namespace Velvet_Site.Pages.Scenes;

public partial class Scene2
{
    private string ActiveAnimationLabel => animationClips?.FirstOrDefault(clip => IsActiveClip(clip))?.Name ?? "Animations";
}
