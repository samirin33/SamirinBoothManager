using UnityEngine.UIElements;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// UpdateInfo.uxml を global::UpdateInfo の内容で埋める。
    /// </summary>
    public class UpdateInfo : SBM_UxmlPartElement
    {
        public new class UxmlFactory : UxmlFactory<UpdateInfo, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        readonly Label _title;
        readonly Label _description;
        readonly Label _date;

        public UpdateInfo() : base(nameof(UpdateInfo))
        {
            _title = this.Q<Label>("Title");
            _description = this.Q<Label>("Discription");
            _date = this.Q<Label>("Date");
        }

        public void Bind(global::UpdateInfo info)
        {
            if (info == null)
                return;

            if (_title != null)
                _title.text = info.updateName ?? string.Empty;

            if (_description != null)
                _description.text = info.updateDescription ?? string.Empty;

            if (_date != null)
                _date.text = FormatDate(info.updateDate);
        }

        static string FormatDate(global::DateTime date)
        {
            if (date == null || date.year <= 0)
                return "----/--/--";
            return $"{date.year:0000}/{date.month:00}/{date.day:00}";
        }
    }
}
