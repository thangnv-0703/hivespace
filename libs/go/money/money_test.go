package money

import "testing"

func TestAdd(t *testing.T) {
	tests := []struct {
		name    string
		a, b    Money
		want    Money
		wantErr bool
	}{
		{"same currency", Money{100, "VND"}, Money{250, "VND"}, Money{350, "VND"}, false},
		{"different currency", Money{100, "VND"}, Money{1, "USD"}, Money{}, true},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got, err := tt.a.Add(tt.b)
			if (err != nil) != tt.wantErr {
				t.Fatalf("err = %v, wantErr %v", err, tt.wantErr)
			}
			if got != tt.want {
				t.Fatalf("got %v, want %v", got, tt.want)
			}
		})
	}
}
